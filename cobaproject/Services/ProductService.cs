using Dapper;
using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Mappers;
using cobaproject.Models;
using cobaproject.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class ProductService : IProductService
{
    private readonly string _connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ProductService> _logger;

    private const string SelectColumns = """
        ID, TITLE, PRICE, DESCRIPTION, CATEGORY, IMAGE,
        RATING_RATE, RATING_COUNT, DISCOUNT_PERCENT, STOCK, IS_ACTIVE, CREATED_AT, CREATED_BY,
        UPDATED_AT, UPDATED_BY, VERSION
        """;

    private const int MaxPageSize = 100;

    private const string EffectivePriceSql = """
        CASE WHEN ISNULL(DISCOUNT_PERCENT, 0) = 0 THEN PRICE
             ELSE ROUND(PRICE - PRICE * ISNULL(DISCOUNT_PERCENT, 0) / 100.0, -2) END
        """;

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "ID",
        ["title"] = "TITLE",
        ["price"] = $"({EffectivePriceSql})",
        ["description"] = "DESCRIPTION",
        ["category"] = "CATEGORY",
        ["image"] = "IMAGE",
        ["ratingRate"] = "RATING_RATE",
        ["ratingCount"] = "RATING_COUNT",
        ["discountPercent"] = "DISCOUNT_PERCENT",
        ["stock"] = "STOCK",
        ["isActive"] = "IS_ACTIVE",
        ["createdAt"] = "CREATED_AT",
        ["createdBy"] = "CREATED_BY",
        ["updatedAt"] = "UPDATED_AT",
        ["updatedBy"] = "UPDATED_BY",
        ["version"] = "VERSION",
    };

    public ProductService(
        IOptions<DatabaseConfig> config,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ProductService> logger)
    {
        _connectionString = config.Value.DefaultConnection;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private string TraceId =>
        _httpContextAccessor.HttpContext?.Items["TraceId"]?.ToString() ?? Guid.NewGuid().ToString();

    public async Task<IEnumerable<ProductDto>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var products = await connection.QueryAsync<MasterProduct>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_PRODUCT
            WHERE IS_ACTIVE = 1
            ORDER BY ID
            """);
        return products.Select(ProductMapper.ToDto);
    }

    public async Task<PagedResult<ProductDto>> GetPagedAsync(ProductQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var conditions = new List<string> { "IS_ACTIVE = 1" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Search))
            AddContainsCondition(conditions, parameters, "Search", """
                (TITLE LIKE @Search ESCAPE '\'
                 OR DESCRIPTION LIKE @Search ESCAPE '\'
                 OR CATEGORY LIKE @Search ESCAPE '\'
                 OR IMAGE LIKE @Search ESCAPE '\'
                 OR CREATED_BY LIKE @Search ESCAPE '\'
                 OR UPDATED_BY LIKE @Search ESCAPE '\'
                 OR CAST(PRICE AS NVARCHAR(50)) LIKE @Search ESCAPE '\'
                 OR CAST(RATING_RATE AS NVARCHAR(50)) LIKE @Search ESCAPE '\'
                 OR CAST(RATING_COUNT AS NVARCHAR(50)) LIKE @Search ESCAPE '\'
                 OR CAST(DISCOUNT_PERCENT AS NVARCHAR(10)) LIKE @Search ESCAPE '\'
                 OR CAST(STOCK AS NVARCHAR(10)) LIKE @Search ESCAPE '\')
                """, query.Search);

        if (!string.IsNullOrWhiteSpace(query.Title))
            AddContainsCondition(conditions, parameters, "Title", "TITLE LIKE @Title ESCAPE '\\'", query.Title);

        if (!string.IsNullOrWhiteSpace(query.Description))
            AddContainsCondition(conditions, parameters, "Description",
                "DESCRIPTION LIKE @Description ESCAPE '\\'", query.Description);

        if (!string.IsNullOrWhiteSpace(query.Category))
            AddContainsCondition(conditions, parameters, "Category",
                "CATEGORY LIKE @Category ESCAPE '\\'", query.Category);

        AddRangeCondition(conditions, parameters, "Price", $"({EffectivePriceSql})", query.PriceFrom, query.PriceTo);
        AddRangeCondition(conditions, parameters, "Stock", "STOCK", query.StockFrom, query.StockTo);
        AddRangeCondition(conditions, parameters, "Created", "CREATED_AT", query.CreatedFrom, query.CreatedTo);
        AddRangeCondition(conditions, parameters, "Updated", "UPDATED_AT", query.UpdatedFrom, query.UpdatedTo);

        var whereClause = string.Join(" AND ", conditions);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            FROM LOSCONSUMER.MASTER_PRODUCT
            WHERE {whereClause};
            """, parameters);

        var sortColumn = SortColumns.TryGetValue(query.SortBy, out var column) ? column : "ID";
        var sortOrder = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var tieBreaker = sortColumn == "ID" ? string.Empty : ", ID";

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var products = await connection.QueryAsync<MasterProduct>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_PRODUCT
            WHERE {whereClause}
            ORDER BY {sortColumn} {sortOrder}{tieBreaker}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters);

        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        return new PagedResult<ProductDto>
        {
            Items = products.Select(ProductMapper.ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = totalPages
        };
    }

    private static void AddContainsCondition(
        List<string> conditions, DynamicParameters parameters, string name, string sql, string value)
    {
        conditions.Add(sql);
        parameters.Add(name, $"%{EscapeLike(value.Trim())}%");
    }

    private static void AddRangeCondition<T>(
        List<string> conditions, DynamicParameters parameters,
        string name, string column, T? from, T? to)
        where T : struct, IComparable<T>
    {
        if (from.HasValue)
        {
            conditions.Add($"{column} >= @{name}From");
            parameters.Add($"{name}From", from.Value);
        }
        if (to.HasValue)
        {
            conditions.Add($"{column} <= @{name}To");
            parameters.Add($"{name}To", to.Value);
        }
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var product = await connection.QueryFirstOrDefaultAsync<MasterProduct>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_PRODUCT
            WHERE ID = @Id AND IS_ACTIVE = 1
            """,
            new { Id = id });
        return product is null ? null : ProductMapper.ToDto(product);
    }

    public async Task<ProductDto?> CreateAsync(CreateProductRequest request, string createdBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var sql = """
            INSERT INTO LOSCONSUMER.MASTER_PRODUCT
                (TITLE, PRICE, DESCRIPTION, CATEGORY, IMAGE,
                 RATING_RATE, RATING_COUNT, DISCOUNT_PERCENT, STOCK, IS_ACTIVE, CREATED_AT, CREATED_BY,
                 UPDATED_AT, UPDATED_BY, VERSION)
            OUTPUT INSERTED.ID
            VALUES
                (@Title, @Price, @Description, @Category, @Image,
                 @RatingRate, @RatingCount, @DiscountPercent, @Stock, 1, GETDATE(), @CreatedBy,
                 NULL, NULL, 1);
            """;

        var newId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            request.Title,
            request.Price,
            request.Description,
            request.Category,
            request.Image,
            request.RatingRate,
            request.RatingCount,
            request.DiscountPercent,
            Stock = request.Stock ?? 0,
            CreatedBy = createdBy
        });

        _logger.LogInformation(
            "[MASTER_PRODUCT] INSERT | ID={Id} | Title=\"{Title}\" | Version=1 | TraceId={TraceId}",
            newId, request.Title, TraceId);

        return await GetByIdAsync(newId);
    }

    public async Task<(ProductDto? Product, bool IsConflict)> UpdateAsync(
        int id, UpdateProductRequest request, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var sql = """
            UPDATE LOSCONSUMER.MASTER_PRODUCT
            SET    TITLE        = @Title,
                   PRICE        = @Price,
                   DESCRIPTION  = @Description,
                   CATEGORY     = @Category,
                   IMAGE        = @Image,
                   RATING_RATE  = @RatingRate,
                   RATING_COUNT = @RatingCount,
                   DISCOUNT_PERCENT = @DiscountPercent,
                   STOCK          = @Stock,
                   UPDATED_AT   = GETDATE(),
                   UPDATED_BY   = @UpdatedBy,
                   VERSION      = VERSION + 1
            WHERE  ID           = @Id
              AND  VERSION      = @Version
              AND  IS_ACTIVE    = 1;
            """;

        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            request.Title,
            request.Price,
            request.Description,
            request.Category,
            request.Image,
            request.RatingRate,
            request.RatingCount,
            request.DiscountPercent,
            Stock = request.Stock ?? 0,
            UpdatedBy = updatedBy,
            Id = id,
            request.Version
        });

        if (rowsAffected == 0)
        {
            var current = await GetByIdAsync(id);
            if (current is null)
                return (null, false);

            _logger.LogWarning(
                "[MASTER_PRODUCT] CONFLICT | ID={Id} | ExpectedVersion={ExpectedVersion} | TraceId={TraceId}",
                id, request.Version, TraceId);

            return (current, true);
        }

        var updated = await GetByIdAsync(id);
        _logger.LogInformation(
            "[MASTER_PRODUCT] UPDATE | ID={Id} | NewVersion={NewVersion} | TraceId={TraceId}",
            id, updated?.Version, TraceId);

        return (updated, false);
    }

    public async Task<bool> SoftDeleteAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var sql = """
            UPDATE LOSCONSUMER.MASTER_PRODUCT
            SET    IS_ACTIVE  = 0,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy
            WHERE  ID         = @Id
              AND  IS_ACTIVE  = 1;
            """;
        var rows = await connection.ExecuteAsync(sql, new { Id = id, UpdatedBy = updatedBy });
        if (rows > 0)
            _logger.LogWarning("[MASTER_PRODUCT] SOFT_DELETE | ID={Id} | TraceId={TraceId}", id, TraceId);
        return rows > 0;
    }

    public async Task<bool> HardDeleteAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.ExecuteAsync(
            "DELETE FROM LOSCONSUMER.MASTER_PRODUCT WHERE ID = @Id;",
            new { Id = id });
        if (rows > 0)
            _logger.LogWarning("[MASTER_PRODUCT] HARD_DELETE | ID={Id} | TraceId={TraceId}", id, TraceId);
        return rows > 0;
    }

    #region Others

    public async Task<List<string>> GetCategoriesAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var categories = await connection.QueryAsync<string>(
            "SELECT DISTINCT CATEGORY FROM LOSCONSUMER.MASTER_PRODUCT WHERE IS_ACTIVE = 1;");
        return categories.ToList();
    }

    #endregion Others
}
