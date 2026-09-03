using System.Text.Json;
using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class AuditLogService : IAuditLogService
{
    private readonly string _connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(IOptions<DatabaseConfig> config, IHttpContextAccessor httpContextAccessor)
    {
        _connectionString = config.Value.DefaultConnection;
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    private string Actor => Context?.Items["Caller"]?.ToString()
        ?? Context?.User.Identity?.Name
        ?? "SYSTEM";

    private string? TraceId => Context?.Items["TraceId"]?.ToString();

    public async Task LogAsync(string entity, string? entityId, string action,
        string? oldSnapshot = null, string? newSnapshot = null, string? reason = null)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync("""
                INSERT INTO LOSCONSUMER.TRX_AUDIT_LOG
                    (ENTITY, ENTITY_ID, ACTION, ACTOR, ACTED_AT, OLD_SNAPSHOT, NEW_SNAPSHOT, REASON, TRACE_ID)
                VALUES
                    (@Entity, @EntityId, @Action, @Actor, GETDATE(), @OldSnapshot, @NewSnapshot, @Reason, @TraceId);
                """, new
            {
                Entity = entity,
                EntityId = entityId,
                Action = action,
                Actor = Actor,
                OldSnapshot = oldSnapshot,
                NewSnapshot = newSnapshot,
                Reason = reason,
                TraceId = TraceId
            });
        }
        catch (Exception)
        {
            // Audit tidak boleh menggagalkan operasi bisnis.
        }
    }

    public static string Json(object? value) =>
        value is null ? "null" : JsonSerializer.Serialize(value);

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "A.ID",
        ["entity"] = "A.ENTITY",
        ["action"] = "A.ACTION",
        ["actor"] = "A.ACTOR",
        ["actedAt"] = "A.ACTED_AT"
    };

    public async Task<PagedResult<AuditLogEntryDto>> GetPagedAsync(AuditLogQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Entity))
        {
            conditions.Add("A.ENTITY = @Entity");
            parameters.Add("Entity", query.Entity.Trim().ToUpperInvariant());
        }
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            conditions.Add("A.ACTION = @Action");
            parameters.Add("Action", query.Action.Trim().ToUpperInvariant());
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("(A.ACTOR LIKE @Search ESCAPE '\\' OR A.REASON LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", $"%{EscapeLike(query.Search.Trim())}%");
        }
        if (query.From.HasValue)
        {
            conditions.Add("A.ACTED_AT >= @From");
            parameters.Add("From", query.From.Value);
        }
        if (query.To.HasValue)
        {
            conditions.Add("A.ACTED_AT <= @To");
            parameters.Add("To", query.To.Value);
        }

        var whereClause = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        var sortColumn = !string.IsNullOrEmpty(query.SortBy)
            && SortColumns.TryGetValue(query.SortBy, out var column) ? column : "A.ACTED_AT";
        var sortOrder = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var tieBreaker = sortColumn == "A.ID" ? string.Empty : ", A.ID";
        var offset = (page - 1) * pageSize;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            FROM LOSCONSUMER.TRX_AUDIT_LOG A
            {whereClause};
            """, parameters);

        var rows = await connection.QueryAsync<AuditLogEntryDto>($"""
            SELECT A.ID, A.ENTITY, A.ENTITY_ID, A.ACTION, A.ACTOR, A.ACTED_AT,
                   A.OLD_SNAPSHOT, A.NEW_SNAPSHOT, A.REASON, A.TRACE_ID
            FROM LOSCONSUMER.TRX_AUDIT_LOG A
            {whereClause}
            ORDER BY {sortColumn} {sortOrder}{tieBreaker}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters);

        return new PagedResult<AuditLogEntryDto>
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}