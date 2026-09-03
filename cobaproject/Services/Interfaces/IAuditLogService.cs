namespace cobaproject.Services.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string entity, string? entityId, string action,
        string? oldSnapshot = null, string? newSnapshot = null, string? reason = null);
}