using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class SettingService : ISettingService
{
    public const string LoginFailThreshold = "LOGIN_FAIL_THRESHOLD";
    public const string ShippingFee = "SHIPPING_FEE";
    public const string TaxPercent = "TAX_PERCENT";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [LoginFailThreshold] = "Ambang Blokir Login",
        [ShippingFee] = "Ongkir",
        [TaxPercent] = "Pajak"
    };

    private readonly string _connectionString;
    private readonly IAuditLogService _audit;

    public SettingService(IOptions<DatabaseConfig> config, IAuditLogService auditLogService)
    {
        _connectionString = config.Value.DefaultConnection;
        _audit = auditLogService;
    }

    public async Task<SettingDto?> GetAsync(string key)
    {
        using var connection = new SqlConnection(_connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<dynamic>("""
            SELECT ID, SETTING_KEY, SETTING_VALUE, UPDATED_AT, UPDATED_BY, VERSION
            FROM LOSCONSUMER.APP_SETTING
            WHERE SETTING_KEY = @Key AND IS_ACTIVE = 1;
            """, new { Key = key });

        if (row is null)
        {
            return null;
        }

        return new SettingDto
        {
            Id = (int)row.ID,
            Key = (string)row.SETTING_KEY,
            Value = (string)row.SETTING_VALUE,
            Label = Labels.TryGetValue((string)row.SETTING_KEY, out var label) ? label : (string)row.SETTING_KEY,
            UpdatedAt = row.UPDATED_AT as DateTime?,
            UpdatedBy = row.UPDATED_BY as string,
            Version = (int)row.VERSION
        };
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        string key, string value, int version, string updatedBy)
    {
        if (!Labels.ContainsKey(key))
        {
            return (false, "Pengaturan tidak dikenali.");
        }

        var error = Validate(key, value);
        if (error is not null)
        {
            return (false, error);
        }

        var normalized = value.Trim();

        using var connection = new SqlConnection(_connectionString);

        var current = await connection.ExecuteScalarAsync<string?>(
            "SELECT SETTING_VALUE FROM LOSCONSUMER.APP_SETTING WHERE SETTING_KEY = @Key AND IS_ACTIVE = 1;",
            new { Key = key });

        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.APP_SETTING
            SET    SETTING_VALUE = @Value,
                   UPDATED_AT    = GETDATE(),
                   UPDATED_BY    = @UpdatedBy,
                   VERSION       = VERSION + 1
            WHERE  SETTING_KEY   = @Key
              AND  VERSION       = @Version
              AND  IS_ACTIVE     = 1;
            """, new { Key = key, Value = normalized, UpdatedBy = updatedBy, Version = version });

        if (affected == 0)
        {
            return (false, "Pengaturan sudah diubah oleh proses lain — muat ulang, lalu simpan lagi.");
        }

        await _audit.LogAsync("SETTING", key, "SETTING_CHANGED",
            AuditLogService.Json(new { key, value = current }),
            AuditLogService.Json(new { key, value = normalized }),
            Labels[key]);

        return (true, null);
    }

    private static string? Validate(string key, string value)
    {
        if (key == LoginFailThreshold)
        {
            if (!int.TryParse(value.Trim(), out var threshold) || threshold is < 1 or > 99)
            {
                return "Ambang blokir harus angka utuh antara 1 dan 99.";
            }
        }
        else if (key == ShippingFee)
        {
            if (!decimal.TryParse(value.Trim(), out var fee) || fee < 0 || fee > 999_999_999)
            {
                return "Ongkir harus angka 0 atau lebih.";
            }
        }
        else if (key == TaxPercent)
        {
            if (!decimal.TryParse(value.Trim(), out var tax) || tax < 0 || tax > 100)
            {
                return "Pajak harus angka antara 0 dan 100.";
            }
        }

        return null;
    }
}