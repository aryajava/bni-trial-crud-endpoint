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

public class UserService : IUserService
{
    private readonly string _connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserService> _logger;

    private const string SelectColumns = """
        ID, USERNAME, PASSWORD_HASH, DISPLAY_NAME, ROLE, SECRET_KEY, LAST_LOGIN_AT,
        LOGIN_FAILED_COUNT, IS_BLOCKED,
        IS_ACTIVE, CREATED_AT, CREATED_BY, UPDATED_AT, UPDATED_BY, VERSION
        """;

    public UserService(
        IOptions<DatabaseConfig> config,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserService> logger)
    {
        _connectionString = config.Value.DefaultConnection;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private string TraceId =>
        _httpContextAccessor.HttpContext?.Items["TraceId"]?.ToString() ?? Guid.NewGuid().ToString();

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var users = await connection.QueryAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER
            ORDER BY ID
            """);
        return users.Select(UserMapper.ToDto);
    }

    public async Task<PagedResult<UserDto>> GetPagedAsync(UserQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            conditions.Add("U.ROLE = @Role");
            parameters.Add("Role", query.Role.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("""
                (U.USERNAME LIKE @Search ESCAPE '\'
                 OR U.DISPLAY_NAME LIKE @Search ESCAPE '\'
                 OR U.ROLE LIKE @Search ESCAPE '\'
                 OR U.CREATED_BY LIKE @Search ESCAPE '\'
                 OR U.UPDATED_BY LIKE @Search ESCAPE '\')
                """);
            parameters.Add("Search", $"%{EscapeLike(query.Search.Trim())}%");
        }

        var whereClause = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);

        var sortColumn = !string.IsNullOrEmpty(query.SortBy)
            && SortColumns.TryGetValue(query.SortBy, out var column) ? column : "ID";
        var sortOrder = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var tieBreaker = sortColumn == "ID" ? string.Empty : ", ID";
        var offset = (page - 1) * pageSize;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            FROM LOSCONSUMER.MASTER_USER U
            {whereClause};
            """, parameters);

        var users = await connection.QueryAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER U
            {whereClause}
            ORDER BY {sortColumn} {sortOrder}{tieBreaker}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters);

        return new PagedResult<UserDto>
        {
            Items = users.Select(UserMapper.ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<UserDto?> GetByIdAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER
            WHERE ID = @Id
            """, new { Id = id });
        return user is null ? null : UserMapper.ToDto(user);
    }

    public async Task<UserDto?> GetByUsernameAsync(string username)
    {
        using var connection = new SqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER
            WHERE USERNAME = @Username
            """, new { Username = username });
        return user is null ? null : UserMapper.ToDto(user);
    }

    public async Task<(UserDto? User, string? Error)> AuthenticateAsync(string username, string password)
    {
        using var connection = new SqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER
            WHERE USERNAME = @Username AND IS_ACTIVE = 1
            """, new { Username = username });

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            // Blokir setelah 5× gagal — hanya user yang benar-benar ada yang dihitung.
            if (user is not null)
            {
                var newCount = user.LoginFailedCount + 1;
                if (newCount >= 5)
                {
                    await connection.ExecuteAsync("""
                        UPDATE LOSCONSUMER.MASTER_USER
                        SET    LOGIN_FAILED_COUNT = 5,
                               IS_BLOCKED         = 1,
                               UPDATED_AT         = GETDATE(),
                               UPDATED_BY         = @UpdatedBy,
                               VERSION            = VERSION + 1
                        WHERE  ID = @Id;
                        """, new { user.Id, UpdatedBy = "SYSTEM" });
                    _logger.LogWarning("[AUTH] Akun diblokir setelah 5× gagal | User={Username} | TraceId={TraceId}",
                        user.Username, TraceId);
                    return (null, "blocked");
                }

                await connection.ExecuteAsync("""
                    UPDATE LOSCONSUMER.MASTER_USER
                    SET    LOGIN_FAILED_COUNT = @NewCount,
                           UPDATED_AT         = GETDATE(),
                           UPDATED_BY         = @UpdatedBy,
                           VERSION            = VERSION + 1
                    WHERE  ID = @Id;
                    """, new { user.Id, NewCount = newCount, UpdatedBy = "SYSTEM" });
            }

            return (null, "invalid");
        }

        if (user.IsBlocked)
        {
            return (null, "blocked");
        }

        await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET    LAST_LOGIN_AT    = GETDATE(),
                   LOGIN_FAILED_COUNT = 0,
                   UPDATED_AT        = GETDATE(),
                   UPDATED_BY        = @UpdatedBy,
                   VERSION           = VERSION + 1
            WHERE ID = @Id
            """, new { user.Id, UpdatedBy = user.Username });

        return (UserMapper.ToDto(user), null);
    }

    /// <summary>Ganti password untuk akun yang diblokir (jalur keluar lockout).</summary>
    public async Task<(bool Success, string? Error)> ChangePasswordBlockedAsync(string username, string newPassword)
    {
        using var connection = new SqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<MasterUser>("""
            SELECT ID, USERNAME, IS_BLOCKED FROM LOSCONSUMER.MASTER_USER
            WHERE USERNAME = @Username AND IS_ACTIVE = 1;
            """, new { Username = username });

        if (user is null || !user.IsBlocked)
            return (false, "Akun tidak ditemukan atau tidak dalam status diblokir.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET    PASSWORD_HASH     = @PasswordHash,
                   LOGIN_FAILED_COUNT = 0,
                   IS_BLOCKED         = 0,
                   UPDATED_AT         = GETDATE(),
                   UPDATED_BY         = @UpdatedBy,
                   VERSION            = VERSION + 1
            WHERE  ID = @Id;
            """, new { user.Id, PasswordHash = passwordHash, UpdatedBy = user.Username });

        _logger.LogWarning("[AUTH] Password diubah lewat jalur ganti-password | User={Username} | TraceId={TraceId}",
            user.Username, TraceId);
        return (true, null);
    }

    /// <summary>Pemilik Toko membuka blokir akun secara manual.</summary>
    public async Task<(bool Success, string? Error)> UnblockAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET    IS_BLOCKED         = 0,
                   LOGIN_FAILED_COUNT = 0,
                   UPDATED_AT         = GETDATE(),
                   UPDATED_BY         = @UpdatedBy,
                   VERSION            = VERSION + 1
            WHERE  ID = @Id AND IS_BLOCKED = 1;
            """, new { Id = id, UpdatedBy = updatedBy });

        return (rows > 0, null);
    }

    public async Task<(bool Success, string? Error)> BlockAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET    IS_BLOCKED         = 1,
                   UPDATED_AT         = GETDATE(),
                   UPDATED_BY         = @UpdatedBy,
                   VERSION            = VERSION + 1
            WHERE  ID = @Id AND IS_BLOCKED = 0;
            """, new { Id = id, UpdatedBy = updatedBy });

        return (rows > 0, null);
    }

    public async Task<(UserDto? User, string? SecretKey, string? Error)> CreateAsync(CreateUserRequest request, string createdBy)
    {
        var existing = await GetByUsernameAsync(request.Username);
        if (existing is not null)
        {
            return (null, null, "Username sudah dipakai.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var secretKey = Guid.NewGuid().ToString("N");

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var id = await connection.ExecuteScalarAsync<int>("""
                INSERT INTO LOSCONSUMER.MASTER_USER (USERNAME, PASSWORD_HASH, DISPLAY_NAME, ROLE, SECRET_KEY, CREATED_BY)
                VALUES (@Username, @PasswordHash, @DisplayName, @Role, @SecretKey, @CreatedBy);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
            {
                request.Username,
                PasswordHash = passwordHash,
                request.DisplayName,
                request.Role,
                SecretKey = secretKey,
                CreatedBy = createdBy
            });

            _logger.LogInformation("[{TraceId}] User {Username} dibuat oleh {Creator}", TraceId, request.Username, createdBy);
            var created = await GetByIdAsync(id);
            return (created, secretKey, null);
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            return (null, null, "Username sudah dipakai.");
        }
    }

    public async Task<UserDto?> GetBySecretKeyAsync(string secretKey)
    {
        using var connection = new SqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER
            WHERE SECRET_KEY = @SecretKey AND IS_ACTIVE = 1
            """, new { SecretKey = secretKey });
        return user is null ? null : UserMapper.ToDto(user);
    }

    public async Task<string?> GetSecretKeyAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<string>(
            "SELECT SECRET_KEY FROM LOSCONSUMER.MASTER_USER WHERE ID = @Id",
            new { Id = id });
    }

    public async Task<(bool Success, string? SecretKey, string? Error)> RegenerateSecretKeyAsync(int id, string updatedBy)
    {
        var secretKey = Guid.NewGuid().ToString("N");

        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET SECRET_KEY = @SecretKey,
                UPDATED_AT = GETDATE(),
                UPDATED_BY = @UpdatedBy,
                VERSION = VERSION + 1
            WHERE ID = @Id
            """, new { Id = id, SecretKey = secretKey, UpdatedBy = updatedBy });
        return affected > 0 ? (true, secretKey, null) : (false, null, "User tidak ditemukan.");
    }

    public async Task<(UserDto? User, bool IsConflict)> UpdateAsync(int id, UpdateUserRequest request, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET DISPLAY_NAME = @DisplayName,
                UPDATED_AT = GETDATE(),
                UPDATED_BY = @UpdatedBy,
                VERSION = VERSION + 1
            WHERE ID = @Id AND VERSION = @Version
            """, new
        {
            Id = id,
            request.DisplayName,
            request.Version,
            UpdatedBy = updatedBy
        });

        var user = await GetByIdAsync(id);
        return (user, affected == 0);
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);

        if (await IsSeededSuperAdminAsync(connection, id))
        {
            return (false, "Akun Super Admin seed tidak dapat dinonaktifkan.");
        }

        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET IS_ACTIVE = 0,
                UPDATED_AT = GETDATE(),
                UPDATED_BY = @UpdatedBy,
                VERSION = VERSION + 1
            WHERE ID = @Id AND IS_ACTIVE = 1
            """, new { Id = id, UpdatedBy = updatedBy });
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> ChangeRoleAsync(int id, string newRole, string updatedBy)
    {
        if (!UserRolePolicy.IsValidRole(newRole))
        {
            return (false, "Jenis role tidak dikenali.");
        }

        using var connection = new SqlConnection(_connectionString);

        if (newRole != UserRolePolicy.Sa && await IsSeededSuperAdminAsync(connection, id))
        {
            return (false, "Akun Super Admin seed tidak dapat diturunkan.");
        }

        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET ROLE = @NewRole,
                UPDATED_AT = GETDATE(),
                UPDATED_BY = @UpdatedBy,
                VERSION = VERSION + 1
            WHERE ID = @Id
            """, new { Id = id, NewRole = newRole, UpdatedBy = updatedBy });
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> SetActiveAsync(int id, bool isActive, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);

        if (!isActive && await IsSeededSuperAdminAsync(connection, id))
        {
            return (false, "Akun Super Admin seed tidak dapat dinonaktifkan.");
        }

        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET IS_ACTIVE = @IsActive,
                UPDATED_AT = GETDATE(),
                UPDATED_BY = @UpdatedBy,
                VERSION = VERSION + 1
            WHERE ID = @Id
            """, new { Id = id, IsActive = isActive, UpdatedBy = updatedBy });
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(int id, string newPassword, string updatedBy)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET PASSWORD_HASH     = @PasswordHash,
                IS_BLOCKED        = 0,
                LOGIN_FAILED_COUNT = 0,
                UPDATED_AT = GETDATE(),
                UPDATED_BY = @UpdatedBy,
                VERSION = VERSION + 1
            WHERE ID = @Id
            """, new { Id = id, PasswordHash = passwordHash, UpdatedBy = updatedBy });
        return (affected > 0, null);
    }

    public async Task<int> CountActiveByRoleAsync(string role)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)
            FROM LOSCONSUMER.MASTER_USER
            WHERE ROLE = @Role AND IS_ACTIVE = 1
            """, new { Role = role });
    }

    private const int MaxPageSize = 100;

    private static async Task<bool> IsSeededSuperAdminAsync(SqlConnection connection, int id)
    {
        return await connection.ExecuteScalarAsync<bool>("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM LOSCONSUMER.MASTER_USER WHERE ID = @Id AND USERNAME = 'sa'
            ) THEN 1 ELSE 0 END;
            """, new { Id = id });
    }

    private static readonly Dictionary<string, string> SortColumns = new()
    {
        ["id"] = "ID",
        ["username"] = "USERNAME",
        ["displayName"] = "DISPLAY_NAME",
        ["role"] = "ROLE",
        ["lastLoginAt"] = "LAST_LOGIN_AT",
        ["createdAt"] = "CREATED_AT",
        ["updatedAt"] = "UPDATED_AT"
    };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}