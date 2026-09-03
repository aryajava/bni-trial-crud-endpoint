using Dapper;
using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Helpers;
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
    private readonly IDiscountApprovalService _discountApprovalService;
    private readonly ILogger<ProductService> _logger;
    private readonly IAuditLogService _audit;

    private const string SelectColumns = """
        P.ID, P.TITLE, P.PRICE, P.DESCRIPTION, C.NAME AS CATEGORY, P.CATEGORY_ID, P.IMAGE,
        P.RATING_RATE, P.RATING_COUNT, P.DISCOUNT_PERCENT, P.STOCK, P.IS_ACTIVE, P.CREATED_AT, P.CREATED_BY,
        P.UPDATED_AT, P.UPDATED_BY, P.VERSION
        """;

    private const string FromProduct = """
        FROM LOSCONSUMER.MASTER_PRODUCT P
        LEFT JOIN LOSCONSUMER.MASTER_CATEGORY C ON C.ID = P.CATEGORY_ID
        """;

    private const int MaxPageSize = 100;

    private const string EffectivePriceSql = """
        CASE WHEN ISNULL(P.DISCOUNT_PERCENT, 0) = 0 THEN P.PRICE
             ELSE ROUND(P.PRICE - P.PRICE * ISNULL(P.DISCOUNT_PERCENT, 0) / 100.0, -2) END
        """;

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "P.ID",
        ["title"] = "P.TITLE",
        ["price"] = $"({EffectivePriceSql})",
        ["description"] = "P.DESCRIPTION",
        ["category"] = "C.NAME",
        ["categoryId"] = "P.CATEGORY_ID",
        ["image"] = "P.IMAGE",
        ["ratingRate"] = "P.RATING_RATE",
        ["ratingCount"] = "P.RATING_COUNT",
        ["discountPercent"] = "P.DISCOUNT_PERCENT",
        ["stock"] = "P.STOCK",
        ["isActive"] = "P.IS_ACTIVE",
        ["createdAt"] = "P.CREATED_AT",
        ["createdBy"] = "P.CREATED_BY",
        ["updatedAt"] = "P.UPDATED_AT",
        ["updatedBy"] = "P.UPDATED_BY",
        ["version"] = "P.VERSION",
    };

    public ProductService(
        IOptions<DatabaseConfig> config,
        IHttpContextAccessor httpContextAccessor,
        IDiscountApprovalService discountApprovalService,
        ILogger<ProductService> logger,
        IAuditLogService auditLogService)
    {
        _connectionString = config.Value.DefaultConnection;
        _httpContextAccessor = httpContextAccessor;
        _discountApprovalService = discountApprovalService;
        _logger = logger;
        _audit = auditLogService;
    }

    private string TraceId =>
        _httpContextAccessor.HttpContext?.Items["TraceId"]?.ToString() ?? Guid.NewGuid().ToString();

    private string Caller =>
        _httpContextAccessor.HttpContext?.Items["Caller"]?.ToString() ?? "SCREEN";

    public async Task<IEnumerable<ProductDto>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var products = await connection.QueryAsync<MasterProduct>($"""
            SELECT {SelectColumns}
            {FromProduct}
            WHERE P.IS_ACTIVE = 1
            ORDER BY P.ID
            """);
        return products.Select(ProductMapper.ToDto);
    }

    public async Task<PagedResult<ProductDto>> GetPagedAsync(ProductQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var conditions = new List<string> { "P.IS_ACTIVE = 1" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Search))
            AddContainsCondition(conditions, parameters, "Search", """
                (P.TITLE LIKE @Search ESCAPE '\'
                 OR P.DESCRIPTION LIKE @Search ESCAPE '\'
                 OR C.NAME LIKE @Search ESCAPE '\'
                 OR P.IMAGE LIKE @Search ESCAPE '\'
                 OR P.CREATED_BY LIKE @Search ESCAPE '\'
                 OR P.UPDATED_BY LIKE @Search ESCAPE '\'
                 OR CAST(P.PRICE AS NVARCHAR(50)) LIKE @Search ESCAPE '\'
                 OR CAST(P.RATING_RATE AS NVARCHAR(50)) LIKE @Search ESCAPE '\'
                 OR CAST(P.RATING_COUNT AS NVARCHAR(50)) LIKE @Search ESCAPE '\'
                 OR CAST(P.DISCOUNT_PERCENT AS NVARCHAR(10)) LIKE @Search ESCAPE '\'
                 OR CAST(P.STOCK AS NVARCHAR(10)) LIKE @Search ESCAPE '\')
                """, query.Search);

        if (!string.IsNullOrWhiteSpace(query.Title))
            AddContainsCondition(conditions, parameters, "Title", "P.TITLE LIKE @Title ESCAPE '\\'", query.Title);

        if (!string.IsNullOrWhiteSpace(query.Description))
            AddContainsCondition(conditions, parameters, "Description",
                "P.DESCRIPTION LIKE @Description ESCAPE '\\'", query.Description);

        if (!string.IsNullOrWhiteSpace(query.Category))
            AddContainsCondition(conditions, parameters, "Category",
                "C.NAME LIKE @Category ESCAPE '\\'", query.Category);

        AddRangeCondition(conditions, parameters, "Price", $"({EffectivePriceSql})", query.PriceFrom, query.PriceTo);
        AddRangeCondition(conditions, parameters, "Stock", "P.STOCK", query.StockFrom, query.StockTo);
        AddRangeCondition(conditions, parameters, "Created", "P.CREATED_AT", query.CreatedFrom, query.CreatedTo);
        AddRangeCondition(conditions, parameters, "Updated", "P.UPDATED_AT", query.UpdatedFrom, query.UpdatedTo);

        var whereClause = string.Join(" AND ", conditions);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            {FromProduct}
            WHERE {whereClause};
            """, parameters);

        var sortColumn = !string.IsNullOrEmpty(query.SortBy)
            && SortColumns.TryGetValue(query.SortBy, out var column) ? column : "P.ID";
        var sortOrder = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var tieBreaker = sortColumn == "P.ID" ? string.Empty : ", P.ID";

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var products = await connection.QueryAsync<MasterProduct>($"""
            SELECT {SelectColumns}
            {FromProduct}
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
            {FromProduct}
            WHERE P.ID = @Id AND P.IS_ACTIVE = 1
            """,
            new { Id = id });
        return product is null ? null : ProductMapper.ToDto(product);
    }

    public async Task<ProductDto?> CreateAsync(CreateProductRequest request, string createdBy)
    {
        using var connection = new SqlConnection(_connectionString);

        // Semua perubahan diskon lewat alur persetujuan — tidak ada bypass peran.
        // Produk dibuat tanpa diskon, lalu permintaan persetujuan menyusul.
        var needsApproval = request.DiscountPercent is not null;

        var sql = """
            INSERT INTO LOSCONSUMER.MASTER_PRODUCT
                (TITLE, PRICE, DESCRIPTION, CATEGORY_ID, IMAGE,
                 RATING_RATE, RATING_COUNT, DISCOUNT_PERCENT, STOCK, IS_ACTIVE, CREATED_AT, CREATED_BY,
                 UPDATED_AT, UPDATED_BY, VERSION)
            OUTPUT INSERTED.ID
            VALUES
                (@Title, @Price, @Description, @CategoryId, @Image,
                 @RatingRate, @RatingCount, @DiscountPercent, @Stock, 1, GETDATE(), @CreatedBy,
                 NULL, NULL, 1);
            """;

        var newId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            request.Title,
            request.Price,
            request.Description,
            request.CategoryId,
            request.Image,
            request.RatingRate,
            request.RatingCount,
            DiscountPercent = needsApproval ? null : request.DiscountPercent,
            Stock = request.Stock ?? 0,
            CreatedBy = createdBy
        });

        if (needsApproval)
        {
            var (_, error) = await _discountApprovalService.RequestAsync(
                newId, null, request.DiscountPercent, createdBy);
            if (error is not null)
                _logger.LogWarning("[TRX_DISCOUNT_APPROVAL] Gagal mengajukan | ProductId={ProductId} | {Error} | TraceId={TraceId}",
                    newId, error, TraceId);
        }

        _logger.LogInformation(
            "[MASTER_PRODUCT] INSERT | ID={Id} | Title=\"{Title}\" | Version=1 | TraceId={TraceId}",
            newId, request.Title, TraceId);

        var created = await GetByIdAsync(newId);
        await _audit.LogAsync("PRODUCT", newId.ToString(), "CREATE", null, AuditLogService.Json(created));
        return created;
    }

    public async Task<(ProductDto? Product, bool IsConflict, string? PendingMessage, bool IsSaved)> UpdateAsync(
        int id, UpdateProductRequest request, string updatedBy)
    {
        var current = await GetByIdAsync(id);
        if (current is null)
            return (null, false, null, false);

        // Diskon yang diubah tidak langsung disimpan: produk tetap memakai diskon
        // lama, permintaan persetujuan diajukan — berlaku untuk semua peran.
        var needsApproval = current.DiscountPercent != request.DiscountPercent;

        if (needsApproval && await _discountApprovalService.HasPendingAsync(id))
            return (current, false, "Produk ini masih memiliki permintaan diskon yang menunggu persetujuan.", false);

        using var connection = new SqlConnection(_connectionString);
        var sql = """
            UPDATE LOSCONSUMER.MASTER_PRODUCT
            SET    TITLE        = @Title,
                   PRICE        = @Price,
                   DESCRIPTION  = @Description,
                   CATEGORY_ID  = @CategoryId,
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
            request.CategoryId,
            request.Image,
            request.RatingRate,
            request.RatingCount,
            DiscountPercent = needsApproval ? current.DiscountPercent : request.DiscountPercent,
            Stock = request.Stock ?? 0,
            UpdatedBy = updatedBy,
            Id = id,
            request.Version
        });

        if (rowsAffected == 0)
        {
            _logger.LogWarning(
                "[MASTER_PRODUCT] CONFLICT | ID={Id} | ExpectedVersion={ExpectedVersion} | TraceId={TraceId}",
                id, request.Version, TraceId);

            return (current, true, null, false);
        }

        var updated = await GetByIdAsync(id);
        await _audit.LogAsync("PRODUCT", id.ToString(), "UPDATE", null, AuditLogService.Json(updated));
        _logger.LogInformation(
            "[MASTER_PRODUCT] UPDATE | ID={Id} | NewVersion={NewVersion} | TraceId={TraceId}",
            id, updated?.Version, TraceId);

        if (needsApproval)
        {
            var (_, error) = await _discountApprovalService.RequestAsync(
                id, current.DiscountPercent, request.DiscountPercent, updatedBy);
            if (error is not null)
            {
                _logger.LogWarning("[TRX_DISCOUNT_APPROVAL] Gagal mengajukan | ProductId={ProductId} | {Error} | TraceId={TraceId}",
                    id, error, TraceId);
                return (updated, false, error, false);
            }

            return (updated, false, "Diskon menunggu persetujuan.", true);
        }

        return (updated, false, null, true);
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
        {
            _logger.LogWarning("[MASTER_PRODUCT] SOFT_DELETE | ID={Id} | TraceId={TraceId}", id, TraceId);
            await _audit.LogAsync("PRODUCT", id.ToString(), "DELETE");
        }
        return rows > 0;
    }

    public async Task<bool> HardDeleteAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.ExecuteAsync(
            "DELETE FROM LOSCONSUMER.MASTER_PRODUCT WHERE ID = @Id;",
            new { Id = id });
        if (rows > 0)
        {
            _logger.LogWarning("[MASTER_PRODUCT] HARD_DELETE | ID={Id} | TraceId={TraceId}", id, TraceId);
            await _audit.LogAsync("PRODUCT", id.ToString(), "DELETE");
        }
        return rows > 0;
    }

    #region Others

    public async Task<List<string>> GetCategoriesAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var categories = await connection.QueryAsync<string>(
            "SELECT NAME FROM LOSCONSUMER.MASTER_CATEGORY WHERE IS_ACTIVE = 1 ORDER BY NAME;");
        return categories.ToList();
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var stats = await connection.QuerySingleAsync<DashboardStatsDto>("""
            SELECT
                COUNT(*)                                             AS Total,
                ISNULL(SUM(CASE WHEN ISNULL(DISCOUNT_PERCENT, 0) > 0 THEN 1 ELSE 0 END), 0) AS Discounted,
                ISNULL(SUM(CASE WHEN STOCK BETWEEN 1 AND 5 THEN 1 ELSE 0 END), 0)          AS LowStock,
                ISNULL(SUM(CASE WHEN STOCK = 0 THEN 1 ELSE 0 END), 0)                       AS OutOfStock
            FROM LOSCONSUMER.MASTER_PRODUCT
            WHERE IS_ACTIVE = 1;
            """);
        return stats;
    }

    #endregion Others
}
