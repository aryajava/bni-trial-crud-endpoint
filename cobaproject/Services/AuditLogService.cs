using System.Text.Json;
using cobaproject.Configuration;
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
}