using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Mappers;
using cobaproject.Models;
using cobaproject.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class DiscountApprovalService : IDiscountApprovalService
{
    public const string Menunggu = "MENUNGGU";
    public const string Disetujui = "DISETUJUI";
    public const string Ditolak = "DITOLAK";
    private const string System = "SISTEM";

    private const string SelectColumns = """
        A.ID, A.PRODUCT_ID, P.TITLE, A.OLD_VALUE, A.NEW_VALUE,
        A.REQUESTED_BY, A.REQUESTED_AT, A.STATUS, A.DECIDED_AT, A.DECIDED_BY, A.REASON
        """;

    private readonly string _connectionString;

    public DiscountApprovalService(IOptions<DatabaseConfig> config)
    {
        _connectionString = config.Value.DefaultConnection;
    }

    public async Task<bool> HasPendingAsync(int productId)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<bool>("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM LOSCONSUMER.TRX_DISCOUNT_APPROVAL
                WHERE PRODUCT_ID = @ProductId AND STATUS = 'MENUNGGU'
            ) THEN 1 ELSE 0 END;
            """, new { ProductId = productId });
    }

    public async Task<(DiscountApprovalDto? Request, string? Error)> RequestAsync(
        int productId, decimal? oldValue, decimal? newValue, string requestedBy)
    {
        using var connection = new SqlConnection(_connectionString);

        if (await HasPendingAsync(productId))
        {
            return (null, "Produk ini masih memiliki permintaan diskon yang menunggu persetujuan.");
        }

        var id = await connection.ExecuteScalarAsync<int>("""
            INSERT INTO LOSCONSUMER.TRX_DISCOUNT_APPROVAL
                (PRODUCT_ID, OLD_VALUE, NEW_VALUE, REQUESTED_BY)
            OUTPUT INSERTED.ID
            VALUES (@ProductId, @OldValue, @NewValue, @RequestedBy);
            """, new { ProductId = productId, OldValue = oldValue, NewValue = newValue, RequestedBy = requestedBy });

        var request = await GetByIdAsync(id);
        return (request, null);
    }

    public async Task<List<DiscountApprovalDto>> GetPendingAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<DiscountApprovalRow>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.TRX_DISCOUNT_APPROVAL A
            JOIN LOSCONSUMER.MASTER_PRODUCT P ON P.ID = A.PRODUCT_ID
            WHERE A.STATUS = 'MENUNGGU'
            ORDER BY A.REQUESTED_AT;
            """);
        return rows.Select(DiscountApprovalMapper.ToDto).ToList();
    }

    public async Task<int> CountPendingAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM LOSCONSUMER.TRX_DISCOUNT_APPROVAL
            WHERE STATUS = 'MENUNGGU';
            """);
    }

    public async Task<List<DiscountApprovalDto>> GetForUserAsync(string requestedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<DiscountApprovalRow>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.TRX_DISCOUNT_APPROVAL A
            JOIN LOSCONSUMER.MASTER_PRODUCT P ON P.ID = A.PRODUCT_ID
            WHERE A.REQUESTED_BY = @RequestedBy
            ORDER BY A.REQUESTED_AT DESC;
            """, new { RequestedBy = requestedBy });
        return rows.Select(DiscountApprovalMapper.ToDto).ToList();
    }

    public async Task<string?> DecideAsync(int id, bool approve, string decidedBy, string? reason)
    {
        using var connection = new SqlConnection(_connectionString);

        var request = await GetByIdAsync(id);
        if (request is null)
            return "Permintaan tidak ditemukan.";

        if (request.Status != Menunggu)
            return "Permintaan sudah diputuskan sebelumnya.";

        var product = await connection.QueryFirstOrDefaultAsync<MasterProduct>("""
            SELECT ID, DISCOUNT_PERCENT, IS_ACTIVE
            FROM LOSCONSUMER.MASTER_PRODUCT
            WHERE ID = @ProductId;
            """, new { request.ProductId });

        // Gugur otomatis: produk hilang/nonaktif atau nilai diskon sudah berubah
        // oleh pihak lain (mis. OWNER/SYSTEM yang bypass) — tidak menimpa apa pun.
        if (product is null || !product.IsActive || product.DiscountPercent != request.OldValue)
        {
            await MarkDecidedAsync(connection, id, Ditolak, System,
                "Nilai diskon produk sudah berubah; permintaan tidak lagi berlaku.");
            return null;
        }

        if (!approve)
        {
            await MarkDecidedAsync(connection, id, Ditolak, decidedBy,
                string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
            return null;
        }

        await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_PRODUCT
            SET    DISCOUNT_PERCENT = @NewValue,
                   UPDATED_AT   = GETDATE(),
                   UPDATED_BY   = @DecidedBy,
                   VERSION      = VERSION + 1
            WHERE  ID           = @ProductId;
            """, new { request.NewValue, DecidedBy = decidedBy, request.ProductId });

        await MarkDecidedAsync(connection, id, Disetujui, decidedBy, null);
        return null;
    }

    private async Task<DiscountApprovalDto?> GetByIdAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<DiscountApprovalRow>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.TRX_DISCOUNT_APPROVAL A
            JOIN LOSCONSUMER.MASTER_PRODUCT P ON P.ID = A.PRODUCT_ID
            WHERE A.ID = @Id;
            """, new { Id = id });
        return row is null ? null : DiscountApprovalMapper.ToDto(row);
    }

    private static async Task MarkDecidedAsync(SqlConnection connection, int id,
        string status, string decidedBy, string? reason)
    {
        await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.TRX_DISCOUNT_APPROVAL
            SET    STATUS     = @Status,
                   DECIDED_AT = GETDATE(),
                   DECIDED_BY = @DecidedBy,
                   REASON     = @Reason
            WHERE  ID         = @Id;
            """, new { Id = id, Status = status, DecidedBy = decidedBy, Reason = reason });
    }
}