using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Helpers;
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
    private const int MaxPageSize = 100;

    private const string SelectColumns = """
        A.ID, A.PRODUCT_ID, P.TITLE, P.PRICE, A.OLD_VALUE, A.NEW_VALUE,
        A.REQUESTED_BY, A.REQUESTED_AT, A.STATUS, A.DECIDED_AT, A.DECIDED_BY, A.REASON,
        A.VERSION
        """;

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "A.ID",
        ["title"] = "P.TITLE",
        ["newValue"] = "A.NEW_VALUE",
        ["status"] = "A.STATUS",
        ["createdAt"] = "A.CREATED_AT",
        ["requestedBy"] = "A.REQUESTED_BY",
        ["reason"] = "A.REASON",
    };

    private readonly string _connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditLogService _audit;

    public DiscountApprovalService(
        IOptions<DatabaseConfig> config,
        IHttpContextAccessor httpContextAccessor,
        IAuditLogService auditLogService)
    {
        _connectionString = config.Value.DefaultConnection;
        _httpContextAccessor = httpContextAccessor;
        _audit = auditLogService;
    }

    private HttpContext HttpContext => _httpContextAccessor.HttpContext!;

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
                (PRODUCT_ID, OLD_VALUE, NEW_VALUE, REQUESTED_BY,
                 IS_ACTIVE, CREATED_AT, CREATED_BY, VERSION)
            OUTPUT INSERTED.ID
            VALUES (@ProductId, @OldValue, @NewValue, @RequestedBy,
                    1, GETDATE(), @RequestedBy, 1);
            """, new { ProductId = productId, OldValue = oldValue, NewValue = newValue, RequestedBy = requestedBy });

        var request = await GetByIdAsync(id);
        await _audit.LogAsync("DISCOUNT_APPROVAL", id.ToString(), "CREATE",
            AuditLogService.Json(new { request?.OldValue, request?.NewValue }), null);
        return (request, null);
    }

    public async Task<PagedResult<DiscountApprovalDto>> GetPagedAsync(ApprovalQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // OWNER melihat semua permintaan; SA dan ADMIN hanya melihat miliknya sendiri.
        if (HttpContext.User.IsInRole(UserRolePolicy.Owner) != true)
        {
            conditions.Add("A.REQUESTED_BY = @RequestedBy");
            parameters.Add("RequestedBy", HttpContext.User.Identity?.Name ?? "?");
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            conditions.Add("A.STATUS = @Status");
            parameters.Add("Status", query.Status.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("""
                (P.TITLE LIKE @Search ESCAPE '\'
                 OR A.REQUESTED_BY LIKE @Search ESCAPE '\')
                """);
            parameters.Add("Search", $"%{EscapeLike(query.Search.Trim())}%");
        }

        var whereClause = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            FROM LOSCONSUMER.TRX_DISCOUNT_APPROVAL A
            JOIN LOSCONSUMER.MASTER_PRODUCT P ON P.ID = A.PRODUCT_ID
            {whereClause};
            """, parameters);

        var sortColumn = !string.IsNullOrEmpty(query.SortBy)
            && SortColumns.TryGetValue(query.SortBy, out var column) ? column : "A.CREATED_AT";
        var sortOrder = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var tieBreaker = sortColumn == "A.ID" ? string.Empty : ", A.ID DESC";

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var rows = await connection.QueryAsync<DiscountApprovalRow>($"""
            SELECT {SelectColumns}
            FROM LOSCONSUMER.TRX_DISCOUNT_APPROVAL A
            JOIN LOSCONSUMER.MASTER_PRODUCT P ON P.ID = A.PRODUCT_ID
            {whereClause}
            ORDER BY {sortColumn} {sortOrder}{tieBreaker}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters);

        return new PagedResult<DiscountApprovalDto>
        {
            Items = rows.Select(DiscountApprovalMapper.ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task<int> CountPendingAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM LOSCONSUMER.TRX_DISCOUNT_APPROVAL
            WHERE STATUS = 'MENUNGGU';
            """);
    }

    public async Task<string?> DecideAsync(int id, bool approve, string decidedBy, string? reason, int version)
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
            var gugur = await MarkDecidedAsync(connection, id, Ditolak, System,
                "Nilai diskon produk sudah berubah; permintaan tidak lagi berlaku.", version, isActive: false);
            if (gugur)
            {
                await _audit.LogAsync("DISCOUNT_APPROVAL", id.ToString(), "REJECT",
                    AuditLogService.Json(new { request.OldValue, request.NewValue }), null,
                    "Nilai diskon produk sudah berubah; permintaan tidak lagi berlaku.");
            }
            return gugur ? null : "Permintaan sudah diubah oleh proses lain. Muat ulang halaman, lalu putuskan kembali.";
        }

        if (!approve)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "Alasan penolakan wajib diisi.";

            var ditolak = await MarkDecidedAsync(connection, id, Ditolak, decidedBy, reason.Trim(), version);
            if (ditolak)
            {
                await _audit.LogAsync("DISCOUNT_APPROVAL", id.ToString(), "REJECT",
                    AuditLogService.Json(new { request.OldValue, request.NewValue }), null, reason.Trim());
            }
            return ditolak ? null : "Permintaan sudah diubah oleh proses lain. Muat ulang halaman, lalu putuskan kembali.";
        }

        // Catatan persetujuan opsional; kosong diberi default agar kolom Catatan terisi.
        // Keputusan dicatat lebih dulu (cek VERSION) — diskon diterapkan setelahnya,
        // sehingga konflik versi tidak meninggalkan produk ter-update tanpa keputusan.
        var setujui = await MarkDecidedAsync(connection, id, Disetujui, decidedBy,
            string.IsNullOrWhiteSpace(reason) ? "Disetujui" : reason.Trim(), version);
        if (!setujui)
            return "Permintaan sudah diubah oleh proses lain. Muat ulang halaman, lalu putuskan kembali.";

        await _audit.LogAsync("DISCOUNT_APPROVAL", id.ToString(), "APPROVE",
            AuditLogService.Json(new { request.OldValue, request.NewValue }),
            AuditLogService.Json(new { request.NewValue }),
            string.IsNullOrWhiteSpace(reason) ? "Disetujui" : reason.Trim());

        var updated = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.MASTER_PRODUCT
            SET    DISCOUNT_PERCENT = @NewValue,
                   UPDATED_AT   = GETDATE(),
                   UPDATED_BY   = @DecidedBy,
                   VERSION      = VERSION + 1
            WHERE  ID           = @ProductId;
            """, new { request.NewValue, DecidedBy = decidedBy, request.ProductId });

        if (updated == 0)
            return "Produk tidak ditemukan atau sudah dinonaktifkan.";

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

    // Optimistik: hanya berhasil bila VERSION sama dengan yang dilihat klien;
    // sekaligus mencatat kolom audit UPDATED_AT/BY dan menaikkan VERSION.
    private static async Task<bool> MarkDecidedAsync(SqlConnection connection, int id,
        string status, string decidedBy, string? reason, int version, bool isActive = true)
    {
        var rows = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.TRX_DISCOUNT_APPROVAL
            SET    STATUS     = @Status,
                   DECIDED_AT = GETDATE(),
                   DECIDED_BY = @DecidedBy,
                   REASON     = @Reason,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @DecidedBy,
                   IS_ACTIVE  = @IsActive,
                   VERSION    = VERSION + 1
            WHERE  ID         = @Id
              AND  VERSION    = @Version;
            """, new { Id = id, Status = status, DecidedBy = decidedBy, Reason = reason, Version = version, IsActive = isActive });
        return rows > 0;
    }
}