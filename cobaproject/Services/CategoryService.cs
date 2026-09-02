using Dapper;
using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Mappers;
using cobaproject.Models;
using cobaproject.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class CategoryService : ICategoryService
{
    private const string SelectColumns = """
        C.ID, C.NAME, C.IS_ACTIVE, C.CREATED_AT, C.CREATED_BY,
        C.UPDATED_AT, C.UPDATED_BY, C.VERSION,
        ISNULL((SELECT COUNT(*) FROM LOSCONSUMER.MASTER_PRODUCT P
                WHERE P.CATEGORY_ID = C.ID AND P.IS_ACTIVE = 1), 0) AS PRODUCT_COUNT
        """;

    private readonly string _connectionString;

    public CategoryService(IOptions<DatabaseConfig> config)
    {
        _connectionString = config.Value.DefaultConnection;
    }

    public async Task<List<CategoryDto>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<MasterCategory>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_CATEGORY C
            ORDER BY C.NAME;
            """);
        return rows.Select(CategoryMapper.ToDto).ToList();
    }

    public async Task<List<CategoryDto>> GetActiveAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<MasterCategory>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_CATEGORY C
            WHERE C.IS_ACTIVE = 1
            ORDER BY C.NAME;
            """);
        return rows.Select(CategoryMapper.ToDto).ToList();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<MasterCategory>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_CATEGORY C
            WHERE C.ID = @Id;
            """, new { Id = id });
        return row is null ? null : CategoryMapper.ToDto(row);
    }

    public async Task<(CategoryDto? Category, string? Error)> CreateAsync(CreateCategoryRequest request, string createdBy)
    {
        using var connection = new SqlConnection(_connectionString);

        if (await NameExistsAsync(connection, request.Name.Trim(), null))
            return (null, "Nama kategori sudah dipakai.");

        var id = await connection.ExecuteScalarAsync<int>("""
            INSERT INTO LOSCONSUMER.MASTER_CATEGORY (NAME, CREATED_AT, CREATED_BY, VERSION)
            OUTPUT INSERTED.ID
            VALUES (@Name, GETDATE(), @CreatedBy, 1);
            """, new { Name = request.Name.Trim(), CreatedBy = createdBy });

        return (await GetByIdAsync(id), null);
    }

    public async Task<(CategoryDto? Category, bool IsConflict, string? Error)> UpdateAsync(
        int id, UpdateCategoryRequest request, string updatedBy)
    {
        var name = request.Name.Trim();

        using var connection = new SqlConnection(_connectionString);

        if (await NameExistsAsync(connection, name, id))
            return (null, false, "Nama kategori sudah dipakai.");

        var rows = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CATEGORY
            SET    NAME       = @Name,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID         = @Id
              AND  VERSION    = @Version;
            """, new { Name = name, UpdatedBy = updatedBy, Id = id, request.Version });

        if (rows == 0)
        {
            var current = await GetByIdAsync(id);
            return (current, true, null);
        }

        return (await GetByIdAsync(id), false, null);
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);

        var used = await connection.ExecuteScalarAsync<bool>("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM LOSCONSUMER.MASTER_PRODUCT
                WHERE CATEGORY_ID = @Id AND IS_ACTIVE = 1
            ) THEN 1 ELSE 0 END;
            """, new { Id = id });

        if (used)
            return (false, "Kategori masih dipakai produk aktif — nonaktifkan produknya dulu.");

        var rows = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CATEGORY
            SET    IS_ACTIVE  = 0,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID         = @Id AND IS_ACTIVE = 1;
            """, new { Id = id, UpdatedBy = updatedBy });

        return (rows > 0, null);
    }

    public async Task<(bool Success, string? Error)> ActivateAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CATEGORY
            SET    IS_ACTIVE  = 1,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID         = @Id AND IS_ACTIVE = 0;
            """, new { Id = id, UpdatedBy = updatedBy });

        return (rows > 0, null);
    }

    private static async Task<bool> NameExistsAsync(SqlConnection connection, string name, int? excludeId)
    {
        return await connection.ExecuteScalarAsync<bool>("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM LOSCONSUMER.MASTER_CATEGORY
                WHERE NAME = @Name AND (@ExcludeId IS NULL OR ID <> @ExcludeId)
            ) THEN 1 ELSE 0 END;
            """, new { Name = name, ExcludeId = excludeId });
    }
}