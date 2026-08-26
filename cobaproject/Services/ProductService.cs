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
        RATING_RATE, RATING_COUNT, IS_ACTIVE, CREATED_AT, CREATED_BY,
        UPDATED_AT, UPDATED_BY, VERSION
        """;

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
                 RATING_RATE, RATING_COUNT, IS_ACTIVE, CREATED_AT, CREATED_BY,
                 UPDATED_AT, UPDATED_BY, VERSION)
            OUTPUT INSERTED.ID
            VALUES
                (@Title, @Price, @Description, @Category, @Image,
                 @RatingRate, @RatingCount, 1, GETDATE(), @CreatedBy,
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
}
