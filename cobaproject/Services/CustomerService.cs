using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Models;
using cobaproject.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class CustomerService : ICustomerService
{
    private const string SelectColumns = """
        ID, EMAIL, PASSWORD_HASH, NAME, PHONE, ADDRESS,
        IS_BLOCKED, IS_ACTIVE, CREATED_AT, CREATED_BY, UPDATED_AT, UPDATED_BY, VERSION
        """;

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "C.ID",
        ["email"] = "C.EMAIL",
        ["name"] = "C.NAME",
        ["createdAt"] = "C.CREATED_AT",
        ["updatedAt"] = "C.UPDATED_AT"
    };

    private readonly string _connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomerService(IOptions<DatabaseConfig> config, IHttpContextAccessor httpContextAccessor)
    {
        _connectionString = config.Value.DefaultConnection;
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    private string TraceId => Context?.Items["TraceId"]?.ToString() ?? Guid.NewGuid().ToString();

    public async Task<CustomerDto?> GetByIdAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<MasterCustomer>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_CUSTOMER
            WHERE ID = @Id;
            """, new { Id = id });
        return row is null ? null : ToDto(row);
    }

    public async Task<CustomerDto?> GetByEmailAsync(string email)
    {
        using var connection = new SqlConnection(_connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<MasterCustomer>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_CUSTOMER
            WHERE EMAIL = @Email;
            """, new { Email = email });
        return row is null ? null : ToDto(row);
    }

    public async Task<PagedResult<CustomerDto>> GetPagedAsync(CustomerQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (query.Active.HasValue)
        {
            conditions.Add("C.IS_ACTIVE = @Active");
            parameters.Add("Active", query.Active.Value);
        }
        if (query.Blocked.HasValue)
        {
            conditions.Add("C.IS_BLOCKED = @Blocked");
            parameters.Add("Blocked", query.Blocked.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("""
                (C.EMAIL LIKE @Search ESCAPE '\'
                 OR C.NAME LIKE @Search ESCAPE '\'
                 OR C.PHONE LIKE @Search ESCAPE '\')
                """);
            parameters.Add("Search", $"%{EscapeLike(query.Search.Trim())}%");
        }

        var whereClause = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        var sortColumn = !string.IsNullOrEmpty(query.SortBy)
            && SortColumns.TryGetValue(query.SortBy, out var column) ? column : "C.ID";
        var sortOrder = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var tieBreaker = sortColumn == "C.ID" ? string.Empty : ", C.ID";
        var offset = (page - 1) * pageSize;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            FROM LOSCONSUMER.MASTER_CUSTOMER C
            {whereClause};
            """, parameters);

        var rows = await connection.QueryAsync<MasterCustomer>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_CUSTOMER C
            {whereClause}
            ORDER BY {sortColumn} {sortOrder}{tieBreaker}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters);

        return new PagedResult<CustomerDto>
        {
            Items = rows.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<(CustomerDto? Customer, string? Error)> RegisterAsync(RegisterCustomerRequest request)
    {
        var email = request.Email.Trim();
        var existing = await GetByEmailAsync(email);
        if (existing is not null)
        {
            return (null, "Email sudah terdaftar.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        using var connection = new SqlConnection(_connectionString);
        int id;
        try
        {
            id = await connection.ExecuteScalarAsync<int>("""
                INSERT INTO LOSCONSUMER.MASTER_CUSTOMER (EMAIL, PASSWORD_HASH, NAME, CREATED_AT, CREATED_BY, VERSION)
                OUTPUT INSERTED.ID
                VALUES (@Email, @PasswordHash, @Name, GETDATE(), @Email, 1);
                """, new { Email = email, PasswordHash = passwordHash, Name = request.Name.Trim() });
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            return (null, "Email sudah terdaftar.");
        }

        await WriteAuditAsync(connection, id, "REGISTER", email, null);

        var created = await GetByIdAsync(id);
        return (created, null);
    }

    public async Task<(CustomerDto? Customer, string? Error)> AuthenticateAsync(string email, string password)
    {
        using var connection = new SqlConnection(_connectionString);
        var customer = await connection.QuerySingleOrDefaultAsync<MasterCustomer>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_CUSTOMER
            WHERE EMAIL = @Email AND IS_ACTIVE = 1;
            """, new { Email = email });

        if (customer is null || !BCrypt.Net.BCrypt.Verify(password, customer.PasswordHash))
        {
            if (customer is not null)
            {
                await WriteAuditAsync(connection, customer.Id, "LOGIN_FAILED", email, null);

                var threshold = await GetLoginFailThresholdAsync(connection);
                var streak = await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)
                    FROM LOSCONSUMER.TRX_CUSTOMER_AUDIT_TRAIL A
                    WHERE A.CUSTOMER_ID = @Id AND A.ACTION = 'LOGIN_FAILED'
                      AND A.ACTED_AT > COALESCE((
                            SELECT MAX(B.ACTED_AT) FROM LOSCONSUMER.TRX_CUSTOMER_AUDIT_TRAIL B
                            WHERE B.CUSTOMER_ID = @Id
                              AND B.ACTION IN ('LOGIN', 'PASSWORD_CHANGED', 'UNBLOCKED')
                      ), '1900-01-01');
                    """, new { customer.Id });

                if (streak >= threshold)
                {
                    await connection.ExecuteAsync("""
                        UPDATE LOSCONSUMER.MASTER_CUSTOMER
                        SET    IS_BLOCKED  = 1,
                               UPDATED_AT = GETDATE(),
                               UPDATED_BY = @UpdatedBy,
                               VERSION    = VERSION + 1
                        WHERE  ID = @Id;
                        """, new { customer.Id, UpdatedBy = "SYSTEM" });
                    return (null, "blocked");
                }
            }

            return (null, "invalid");
        }

        if (customer.IsBlocked)
        {
            return (null, "blocked");
        }

        await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CUSTOMER
            SET    UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID = @Id;
            """, new { customer.Id, UpdatedBy = email });

        await WriteAuditAsync(connection, customer.Id, "LOGIN", email, null);

        return (ToDto(customer), null);
    }

    public async Task<(bool Success, string? Error)> ChangePasswordBlockedAsync(string email, string newPassword)
    {
        using var connection = new SqlConnection(_connectionString);
        var customer = await connection.QuerySingleOrDefaultAsync<MasterCustomer>("""
            SELECT ID, EMAIL, IS_BLOCKED FROM LOSCONSUMER.MASTER_CUSTOMER
            WHERE EMAIL = @Email AND IS_ACTIVE = 1;
            """, new { Email = email });

        if (customer is null || !customer.IsBlocked)
        {
            return (false, "Akun tidak ditemukan atau tidak dalam status diblokir.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CUSTOMER
            SET    PASSWORD_HASH = @PasswordHash,
                   IS_BLOCKED    = 0,
                   UPDATED_AT    = GETDATE(),
                   UPDATED_BY    = @UpdatedBy,
                   VERSION       = VERSION + 1
            WHERE  ID = @Id;
            """, new { customer.Id, PasswordHash = passwordHash, UpdatedBy = email });

        await WriteAuditAsync(connection, customer.Id, "PASSWORD_CHANGED", email, null);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(
        int id, UpdateCustomerProfileRequest request, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CUSTOMER
            SET    NAME       = @Name,
                   PHONE      = @Phone,
                   ADDRESS    = @Address,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID = @Id;
            """, new { request.Name, request.Phone, request.Address, Id = id, UpdatedBy = updatedBy });

        if (affected > 0)
        {
            await WriteAuditAsync(connection, id, "PROFILE_UPDATED", updatedBy, null);
        }
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> BlockAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CUSTOMER
            SET    IS_BLOCKED  = 1,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID = @Id AND IS_BLOCKED = 0 AND IS_ACTIVE = 1;
            """, new { Id = id, UpdatedBy = updatedBy });

        if (affected > 0)
        {
            await WriteAuditAsync(connection, id, "BLOCKED", updatedBy, null);
        }
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> UnblockAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CUSTOMER
            SET    IS_BLOCKED  = 0,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID = @Id AND IS_BLOCKED = 1;
            """, new { Id = id, UpdatedBy = updatedBy });

        if (affected > 0)
        {
            await WriteAuditAsync(connection, id, "UNBLOCKED", updatedBy, null);
        }
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> DeactivateAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CUSTOMER
            SET    IS_ACTIVE  = 0,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID = @Id AND IS_ACTIVE = 1;
            """, new { Id = id, UpdatedBy = updatedBy });

        if (affected > 0)
        {
            await WriteAuditAsync(connection, id, "DEACTIVATED", updatedBy, null);
        }
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> ReactivateAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CUSTOMER
            SET    IS_ACTIVE  = 1,
                   IS_BLOCKED = 0,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID = @Id AND IS_ACTIVE = 0;
            """, new { Id = id, UpdatedBy = updatedBy });

        if (affected > 0)
        {
            await WriteAuditAsync(connection, id, "REACTIVATED", updatedBy, null);
        }
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(int id, string newPassword, string updatedBy)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_CUSTOMER
            SET    PASSWORD_HASH = @PasswordHash,
                   IS_BLOCKED    = 0,
                   UPDATED_AT    = GETDATE(),
                   UPDATED_BY    = @UpdatedBy,
                   VERSION       = VERSION + 1
            WHERE  ID = @Id;
            """, new { Id = id, PasswordHash = passwordHash, UpdatedBy = updatedBy });

        if (affected > 0)
        {
            await WriteAuditAsync(connection, id, "RESET_PASSWORD", updatedBy, null);
            await WriteAuditAsync(connection, id, "UNBLOCKED", updatedBy, null);
        }
        return (affected > 0, null);
    }

    private async Task WriteAuditAsync(SqlConnection connection, int customerId, string action, string actor, string? reason)
    {
        try
        {
            await connection.ExecuteAsync("""
                INSERT INTO LOSCONSUMER.TRX_CUSTOMER_AUDIT_TRAIL (CUSTOMER_ID, ACTION, ACTOR, ACTED_AT, REASON)
                VALUES (@CustomerId, @Action, @Actor, GETDATE(), @Reason);
                """, new { CustomerId = customerId, Action = action, Actor = actor, Reason = reason });
        }
        catch (Exception)
        {
        }
    }

    private static async Task<int> GetLoginFailThresholdAsync(SqlConnection connection)
    {
        var value = await connection.ExecuteScalarAsync<string>("""
            SELECT SETTING_VALUE FROM LOSCONSUMER.APP_SETTING
            WHERE SETTING_KEY = @Key AND IS_ACTIVE = 1;
            """, new { Key = SettingService.LoginFailThreshold });
        return int.TryParse(value, out var threshold) ? threshold : 5;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static CustomerDto ToDto(MasterCustomer c) => new()
    {
        Id = c.Id,
        Email = c.Email,
        Name = c.Name,
        Phone = c.Phone,
        Address = c.Address,
        IsBlocked = c.IsBlocked,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        UpdatedBy = c.UpdatedBy,
        Version = c.Version
    };
}