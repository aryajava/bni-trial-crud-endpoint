using Dapper;
using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Helpers;
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
        ID, USERNAME, PASSWORD_HASH, DISPLAY_NAME, ROLE, LAST_LOGIN_AT,
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

    private static UserDto ToDto(MasterUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        DisplayName = user.DisplayName,
        Role = user.Role,
        LastLoginAt = user.LastLoginAt,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        CreatedBy = user.CreatedBy,
        UpdatedAt = user.UpdatedAt,
        UpdatedBy = user.UpdatedBy,
        Version = user.Version
    };

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var users = await connection.QueryAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER
            ORDER BY ID
            """);
        return users.Select(ToDto);
    }

    public async Task<UserDto?> GetByIdAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER
            WHERE ID = @Id
            """, new { Id = id });
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto?> GetByUsernameAsync(string username)
    {
        using var connection = new SqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER
            WHERE USERNAME = @Username
            """, new { Username = username });
        return user is null ? null : ToDto(user);
    }

    public async Task<(UserDto? User, string? Error)> AuthenticateAsync(string username, string password)
    {
        using var connection = new SqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<MasterUser>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.MASTER_USER
            WHERE USERNAME = @Username
            """, new { Username = username });

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return (null, "invalid");
        }

        if (!user.IsActive)
        {
            return (null, "inactive");
        }

        await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_USER
            SET LAST_LOGIN_AT = GETDATE()
            WHERE ID = @Id
            """, new { user.Id });

        return (ToDto(user), null);
    }

    public async Task<(UserDto? User, string? Error)> CreateAsync(CreateUserRequest request, string createdBy)
    {
        var existing = await GetByUsernameAsync(request.Username);
        if (existing is not null)
        {
            return (null, "Username sudah dipakai.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var id = await connection.ExecuteScalarAsync<int>("""
                INSERT INTO LOSCONSUMER.MASTER_USER (USERNAME, PASSWORD_HASH, DISPLAY_NAME, ROLE, CREATED_BY)
                VALUES (@Username, @PasswordHash, @DisplayName, @Role, @CreatedBy);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
            {
                request.Username,
                PasswordHash = passwordHash,
                request.DisplayName,
                request.Role,
                CreatedBy = createdBy
            });

            _logger.LogInformation("[{TraceId}] User {Username} dibuat oleh {Creator}", TraceId, request.Username, createdBy);
            return (await GetByIdAsync(id), null);
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            return (null, "Username sudah dipakai.");
        }
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
            return (false, "Role tidak valid.");
        }

        using var connection = new SqlConnection(_connectionString);
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
            SET PASSWORD_HASH = @PasswordHash,
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
}