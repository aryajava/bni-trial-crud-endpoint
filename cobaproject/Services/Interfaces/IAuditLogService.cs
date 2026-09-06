namespace cobaproject.Services.Interfaces;

using cobaproject.Dtos;

public interface IAuditLogService
{
    Task LogAsync(string entity, string? entityId, string action,
        string? oldSnapshot = null, string? newSnapshot = null, string? reason = null);

    Task<PagedResult<AuditLogEntryDto>> GetPagedAsync(AuditLogQueryParams query);

    Task<PagedResult<CustomerAuditEntryDto>> GetCustomerPagedAsync(CustomerAuditQueryParams query);

    Task<PagedResult<HttpLogEntryDto>> GetHttpPagedAsync(HttpLogQueryParams query);
}