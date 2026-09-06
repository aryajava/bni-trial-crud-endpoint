using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Serilog;

namespace cobaproject.Services;

public class CourierService : ICourierService
{
    private const string SelectColumns = """
        C.ID, C.NAME, C.SHIPPING_FEE, C.IS_ACTIVE,
        C.CREATED_AT, C.CREATED_BY, C.UPDATED_AT, C.UPDATED_BY, C.VERSION
        """;

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "C.ID",
        ["name"] = "C.NAME",
        ["shippingFee"] = "C.SHIPPING_FEE",
        ["createdAt"] = "C.CREATED_AT",
        ["updatedAt"] = "C.UPDATED_AT"
    };

    private const int MaxPageSize = 100;

    private readonly string _connectionString;
    private readonly IAuditLogService _audit;

    public CourierService(IOptions<DatabaseConfig> config, IAuditLogService auditLogService)
    {
        _connectionString = config.Value.DefaultConnection;
        _audit = auditLogService;
    }

    public async Task<List<CourierDto>> GetActiveAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<CourierDto>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_COURIER C
            WHERE C.IS_ACTIVE = 1
            ORDER BY C.ID;
            """);
        return rows.ToList();
    }

    public async Task<decimal> GetDefaultShippingFeeAsync()
    {
        var courier = await GetDefaultAsync();
        return courier?.ShippingFee ?? 0m;
    }

    public async Task<CourierDto?> GetDefaultAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<CourierDto>($"""
            SELECT TOP 1 {SelectColumns}
            FROM LOSCONSUMER.MASTER_COURIER C
            WHERE C.IS_ACTIVE = 1
            ORDER BY C.ID;
            """);
    }

    public async Task<PagedResult<CourierDto>> GetPagedAsync(CourierQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (query.Active.HasValue)
        {
            conditions.Add("C.IS_ACTIVE = @Active");
            parameters.Add("Active", query.Active.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("(C.NAME LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", $"%{EscapeLike(query.Search.Trim())}%");
        }

        var whereClause = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        var sortColumn = !string.IsNullOrEmpty(query.SortBy)
            && SortColumns.TryGetValue(query.SortBy, out var column) ? column : "C.NAME";
        var sortOrder = query.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var tieBreaker = sortColumn == "C.ID" ? string.Empty : ", C.ID";
        var offset = (page - 1) * pageSize;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            FROM LOSCONSUMER.MASTER_COURIER C
            {whereClause};
            """, parameters);

        var rows = await connection.QueryAsync<CourierDto>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_COURIER C
            {whereClause}
            ORDER BY {sortColumn} {sortOrder}{tieBreaker}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters);

        return new PagedResult<CourierDto>
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<CourierDto?> GetByIdAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<CourierDto>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_COURIER C
            WHERE C.ID = @Id;
            """, new { Id = id });
        return row;
    }

    public async Task<(CourierDto? Courier, string? Error)> CreateAsync(CreateCourierRequest request, string createdBy)
    {
        var name = request.Name.Trim();
        using var connection = new SqlConnection(_connectionString);

        if (await NameExistsAsync(connection, name, null))
        {
            return (null, "Nama ekspedisi sudah dipakai.");
        }

        var id = await connection.ExecuteScalarAsync<int>("""
            INSERT INTO LOSCONSUMER.MASTER_COURIER (NAME, SHIPPING_FEE, CREATED_AT, CREATED_BY, VERSION)
            OUTPUT INSERTED.ID
            VALUES (@Name, @ShippingFee, GETDATE(), @CreatedBy, 1);
            """, new { Name = name, request.ShippingFee, CreatedBy = createdBy });

        var created = await GetByIdAsync(id);
        if (created is not null)
        {
            Log.Information("[COURIER] CREATE | ID={Id} | Name=\"{Name}\" | Ongkir={Fee} | By={By}",
                created.Id, created.Name, created.ShippingFee, createdBy);
            await _audit.LogAsync("COURIER", created.Id.ToString(), "CREATE", null, AuditLogService.Json(created));
        }
        return (created, null);
    }

    public async Task<(CourierDto? Courier, bool IsConflict, string? Error)> UpdateAsync(
        int id, UpdateCourierRequest request, string updatedBy)
    {
        var name = request.Name.Trim();

        using var connection = new SqlConnection(_connectionString);

        if (await NameExistsAsync(connection, name, id))
        {
            return (null, false, "Nama ekspedisi sudah dipakai.");
        }

        var rows = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_COURIER
            SET    NAME        = @Name,
                   SHIPPING_FEE = @ShippingFee,
                   UPDATED_AT  = GETDATE(),
                   UPDATED_BY  = @UpdatedBy,
                   VERSION     = VERSION + 1
            WHERE  ID          = @Id
              AND  VERSION     = @Version;
            """, new { Name = name, request.ShippingFee, UpdatedBy = updatedBy, Id = id, request.Version });

        if (rows == 0)
        {
            var current = await GetByIdAsync(id);
            return (current, true, null);
        }

        var latest = await GetByIdAsync(id);
        if (latest is not null)
        {
            Log.Information("[COURIER] UPDATE | ID={Id} | Name=\"{Name}\" | Ongkir={Fee} | By={By}",
                id, latest.Name, latest.ShippingFee, updatedBy);
            await _audit.LogAsync("COURIER", id.ToString(), "UPDATE", null, AuditLogService.Json(latest));
        }
        return (latest, false, null);
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_COURIER
            SET    IS_ACTIVE  = 0,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID         = @Id AND IS_ACTIVE = 1;
            """, new { Id = id, UpdatedBy = updatedBy });

        if (rows > 0)
        {
            Log.Information("[COURIER] DELETE | ID={Id} | By={By}", id, updatedBy);
            await _audit.LogAsync("COURIER", id.ToString(), "DELETE");
        }
        return (rows > 0, null);
    }

    public async Task<(bool Success, string? Error)> ActivateAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_COURIER
            SET    IS_ACTIVE  = 1,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID         = @Id AND IS_ACTIVE = 0;
            """, new { Id = id, UpdatedBy = updatedBy });

        if (rows > 0)
        {
            await _audit.LogAsync("COURIER", id.ToString(), "UPDATE");
        }
        return (rows > 0, null);
    }

    private static async Task<bool> NameExistsAsync(SqlConnection connection, string name, int? excludeId)
    {
        return await connection.ExecuteScalarAsync<bool>("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM LOSCONSUMER.MASTER_COURIER
                WHERE NAME = @Name AND (@ExcludeId IS NULL OR ID <> @ExcludeId)
            ) THEN 1 ELSE 0 END;
            """, new { Name = name, ExcludeId = excludeId });
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}