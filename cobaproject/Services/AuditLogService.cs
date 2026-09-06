using System.Text.Json;
using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Serilog;

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

        Log.ForContext("SourceContext", "Audit")
            .Information("[{Entity}] {Action} | ID={EntityId} | Actor={Actor} | Trace={TraceId} | Reason={Reason}",
                entity, action, entityId ?? "-", Actor, TraceId ?? "-", reason ?? "-");
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
            var to = query.To.Value;
            if (to.TimeOfDay == TimeSpan.Zero)
            {
                to = to.Date.AddDays(1);
            }
            conditions.Add("A.ACTED_AT < @To");
            parameters.Add("To", to);
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

    private static readonly Dictionary<string, string> CustomerSortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "A.ID",
        ["customerId"] = "A.CUSTOMER_ID",
        ["action"] = "A.ACTION",
        ["actor"] = "A.ACTOR",
        ["actedAt"] = "A.ACTED_AT"
    };

    public async Task<PagedResult<CustomerAuditEntryDto>> GetCustomerPagedAsync(CustomerAuditQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            conditions.Add("A.ACTION = @Action");
            parameters.Add("Action", query.Action.Trim().ToUpperInvariant());
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("(A.ACTOR LIKE @Search ESCAPE '\\' OR C.EMAIL LIKE @Search ESCAPE '\\' OR C.NAME LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", $"%{EscapeLike(query.Search.Trim())}%");
        }
        if (query.From.HasValue)
        {
            conditions.Add("A.ACTED_AT >= @From");
            parameters.Add("From", query.From.Value);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value;
            if (to.TimeOfDay == TimeSpan.Zero)
            {
                to = to.Date.AddDays(1);
            }
            conditions.Add("A.ACTED_AT < @To");
            parameters.Add("To", to);
        }

        var whereClause = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        var sortColumn = !string.IsNullOrEmpty(query.SortBy)
            && CustomerSortColumns.TryGetValue(query.SortBy, out var column) ? column : "A.ACTED_AT";
        var sortOrder = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var tieBreaker = sortColumn == "A.ID" ? string.Empty : ", A.ID";
        var offset = (page - 1) * pageSize;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            FROM LOSCONSUMER.TRX_CUSTOMER_AUDIT_TRAIL A
            JOIN LOSCONSUMER.MASTER_CUSTOMER C ON C.ID = A.CUSTOMER_ID
            {whereClause};
            """, parameters);

        var rows = await connection.QueryAsync<CustomerAuditEntryDto>($"""
            SELECT A.ID, A.CUSTOMER_ID, C.EMAIL AS CUSTOMER_EMAIL, A.ACTION, A.ACTOR, A.ACTED_AT,
                   A.DETAIL, A.REASON
            FROM LOSCONSUMER.TRX_CUSTOMER_AUDIT_TRAIL A
            JOIN LOSCONSUMER.MASTER_CUSTOMER C ON C.ID = A.CUSTOMER_ID
            {whereClause}
            ORDER BY {sortColumn} {sortOrder}{tieBreaker}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters);

        return new PagedResult<CustomerAuditEntryDto>
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    private static readonly Dictionary<string, string> HttpSortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "R.ID",
        ["traceId"] = "R.TRACE_ID",
        ["endpoint"] = "R.ENDPOINT",
        ["httpMethod"] = "R.HTTP_METHOD",
        ["statusCode"] = "RS.STATUS_CODE",
        ["requestedAt"] = "R.REQUESTED_AT",
        ["respondedAt"] = "RS.RESPONDED_AT",
        ["elapsedMs"] = "RS.ELAPSED_MS"
    };

    public async Task<PagedResult<HttpLogEntryDto>> GetHttpPagedAsync(HttpLogQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Path))
        {
            conditions.Add("R.ENDPOINT LIKE @Path ESCAPE '\\'");
            parameters.Add("Path", $"%{EscapeLike(query.Path.Trim())}%");
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("(R.TRACE_ID LIKE @Search ESCAPE '\\' OR R.ENDPOINT LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", $"%{EscapeLike(query.Search.Trim())}%");
        }
        if (!string.IsNullOrWhiteSpace(query.HttpMethod))
        {
            conditions.Add("R.HTTP_METHOD = @HttpMethod");
            parameters.Add("HttpMethod", query.HttpMethod.Trim().ToUpperInvariant());
        }
        if (query.IsSuccess.HasValue)
        {
            conditions.Add("RS.IS_SUCCESS = @IsSuccess");
            parameters.Add("IsSuccess", query.IsSuccess.Value);
        }
        if (query.From.HasValue)
        {
            conditions.Add("R.REQUESTED_AT >= @From");
            parameters.Add("From", query.From.Value);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value;
            if (to.TimeOfDay == TimeSpan.Zero)
            {
                to = to.Date.AddDays(1);
            }
            conditions.Add("R.REQUESTED_AT < @To");
            parameters.Add("To", to);
        }

        var whereClause = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        var sortColumn = !string.IsNullOrEmpty(query.SortBy)
            && HttpSortColumns.TryGetValue(query.SortBy, out var column) ? column : "R.REQUESTED_AT";
        var sortOrder = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var tieBreaker = sortColumn == "R.ID" ? string.Empty : ", R.ID DESC";
        var offset = (page - 1) * pageSize;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            FROM LOSCONSUMER.REQUEST_PRODUCT R
            LEFT JOIN LOSCONSUMER.RESPONSE_PRODUCT RS ON RS.TRACE_ID = R.TRACE_ID
            {whereClause};
            """, parameters);

        var rows = await connection.QueryAsync<HttpLogEntryDto>($"""
            SELECT R.ID, R.TRACE_ID, R.ENDPOINT, R.HTTP_METHOD, R.HEADERS, R.QUERY_PARAMS,
                   R.BODY AS REQUEST_BODY, R.IP_ADDRESS, R.REQUESTED_AT,
                   RS.STATUS_CODE, RS.IS_SUCCESS, RS.MESSAGE AS RESPONSE_MESSAGE,
                   RS.RESPONSE_BODY, RS.ELAPSED_MS, RS.RESPONDED_AT
            FROM LOSCONSUMER.REQUEST_PRODUCT R
            LEFT JOIN LOSCONSUMER.RESPONSE_PRODUCT RS ON RS.TRACE_ID = R.TRACE_ID
            {whereClause}
            ORDER BY {sortColumn} {sortOrder}{tieBreaker}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters);

        return new PagedResult<HttpLogEntryDto>
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }
}