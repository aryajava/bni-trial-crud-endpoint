using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Serilog;

namespace cobaproject.Services;

public class OrderService : IOrderService
{
    public const string MenungguKonfirmasi = "MENUNGGU_KONFIRMASI";
    public const string Dikemas = "DIKEMAS";
    public const string Dikirim = "DIKIRIM";
    public const string Diterima = "DITERIMA";
    public const string Dibatalan = "DIBATALKAN";

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "O.ID",
        ["status"] = "O.STATUS",
        ["totalAmount"] = "O.TOTAL_AMOUNT",
        ["createdAt"] = "O.CREATED_AT",
        ["customerName"] = "ISNULL(C.NAME, C.EMAIL)"
    };

    private readonly string _connectionString;
    private readonly ICartService _cartService;
    private readonly ISettingService _settingService;
    private readonly ICourierService _courierService;
    private readonly IAuditLogService _audit;

    public OrderService(
        IOptions<DatabaseConfig> config,
        ICartService cartService,
        ISettingService settingService,
        ICourierService courierService,
        IAuditLogService auditLogService)
    {
        _connectionString = config.Value.DefaultConnection;
        _cartService = cartService;
        _settingService = settingService;
        _courierService = courierService;
        _audit = auditLogService;
    }

    public async Task<(OrderDetailDto? Order, string? Error)> CheckoutAsync(
        int customerId, CheckoutRequest request, string createdBy)
    {
        var items = (await _cartService.GetAsync(customerId)).Where(i => i.IsAvailable).ToList();
        if (items.Count == 0)
        {
            return (null, "Keranjang tidak memiliki item yang tersedia.");
        }

        var selectedIds = (request.SelectedIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .Distinct()
            .ToHashSet();
        if (selectedIds.Count > 0)
        {
            items = items.Where(i => selectedIds.Contains(i.ProductId)).ToList();
            if (items.Count == 0)
            {
                return (null, "Tidak ada item terpilih yang tersedia untuk dipesan.");
            }
        }

        var subtotal = Math.Round(items.Sum(i => i.Subtotal), 2);
        var courier = request.CourierId.HasValue
            ? await _courierService.GetByIdAsync(request.CourierId.Value)
            : null;
        if (courier is null || !courier.IsActive)
        {
            courier = await _courierService.GetDefaultAsync();
        }
        var shipping = Math.Round(courier?.ShippingFee ?? 0m, 2);
        var taxPercent = decimal.TryParse((await _settingService.GetAsync(SettingService.TaxPercent))?.Value, out var tax)
            ? tax : 0m;
        var taxAmount = Math.Round(subtotal * taxPercent / 100m, 2);
        var total = Math.Round(subtotal + shipping + taxAmount, 2);

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        foreach (var item in items)
        {
            var stock = await connection.ExecuteScalarAsync<int>(
                "SELECT ISNULL(STOCK, 0) FROM LOSCONSUMER.MASTER_PRODUCT WHERE ID = @Id AND IS_ACTIVE = 1;",
                new { Id = item.ProductId }, transaction);
            if (stock < item.Quantity)
            {
                return (null, $"Stok produk \"{item.Title}\" tidak cukup (tersisa {stock}).");
            }
        }

        long? orderId = null;
        string? nomorPesanan = null;
        string? errorBuat = null;
        for (var upaya = 0; upaya < 5 && orderId is null && errorBuat is null; upaya++)
        {
            try
            {
                nomorPesanan = await BangunNomorPesananAsync(connection, transaction);
                orderId = await connection.ExecuteScalarAsync<long>("""
                    INSERT INTO LOSCONSUMER.TRX_ORDER
                        (CUSTOMER_ID, STATUS, ORDER_NUMBER, SUBTOTAL, SHIPPING_FEE, TAX_AMOUNT, TOTAL_AMOUNT,
                         COURIER_ID, COURIER_NAME,
                         SHIP_NAME, SHIP_PHONE, SHIP_ADDRESS, NOTE, CREATED_BY, VERSION)
                    OUTPUT INSERTED.ID
                    VALUES
                        (@CustomerId, 'MENUNGGU_KONFIRMASI', @OrderNumber, @Subtotal, @Shipping, @Tax, @Total,
                         @CourierId, @CourierName,
                         @ShipName, @ShipPhone, @ShipAddress, @Note, @CreatedBy, 1);
                    """, new
                {
                    CustomerId = customerId,
                    OrderNumber = nomorPesanan,
                    Subtotal = subtotal,
                    Shipping = shipping,
                    Tax = taxAmount,
                    Total = total,
                    CourierId = courier?.Id,
                    CourierName = courier?.Name,
                    ShipName = request.Name,
                    ShipPhone = request.Phone,
                    ShipAddress = request.Address,
                    Note = request.Note,
                    CreatedBy = createdBy
                }, transaction);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
            {
                // Tabrakan nomor (dua checkout di milidetik sama): tunggu sejenak agar basis berganti.
                Log.ForContext("SourceContext", "Audit").Warning(
                    "Tabrakan nomor pesanan {Nomor}, mencoba ulang ({Upaya}/5)", nomorPesanan, upaya + 1);
                await Task.Delay(1);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1205)
            {
                Log.ForContext("SourceContext", "Audit").Warning(
                    "Deadlock saat membuat pesanan | CustomerId={CustomerId} | {Pesan}", customerId, ex.Message);
                errorBuat = "Sistem sedang sibuk; coba lagi.";
            }
        }
        if (orderId is null || nomorPesanan is null)
        {
            return (null, errorBuat ?? "Gagal membuat nomor pesanan; coba lagi.");
        }
        var idPesanan = orderId.Value;

        // Pesanan memakai OrderNumber di semua komunikasi ke pelanggan; ID tetap internal.
        foreach (var item in items)
        {
            var lineTotal = Math.Round(item.Subtotal, 2);
            await connection.ExecuteAsync("""
                INSERT INTO LOSCONSUMER.TRX_ORDER_ITEM
                    (ORDER_ID, PRODUCT_ID, TITLE, UNIT_PRICE, DISCOUNT_PERCENT, QUANTITY, SUBTOTAL)
                VALUES
                    (@OrderId, @ProductId, @Title, @UnitPrice, @DiscountPercent, @Quantity, @Subtotal);
                """, new
            {
                OrderId = idPesanan,
                item.ProductId,
                item.Title,
                UnitPrice = item.EffectivePrice,
                item.DiscountPercent,
                item.Quantity,
                Subtotal = lineTotal
            }, transaction);

            var affected = await connection.ExecuteAsync("""
                UPDATE LOSCONSUMER.MASTER_PRODUCT
                SET STOCK = STOCK - @Quantity
                WHERE ID = @Id AND STOCK >= @Quantity;
                """, new { Quantity = item.Quantity, Id = item.ProductId }, transaction);

            if (affected == 0)
            {
                return (null, $"Stok produk \"{item.Title}\" tidak cukup saat konfirmasi.");
            }
        }

        await connection.ExecuteAsync("""
            DELETE FROM LOSCONSUMER.TRX_CART_ITEM
            WHERE CUSTOMER_ID = @CustomerId AND PRODUCT_ID IN @ProductIds;
            """, new { CustomerId = customerId, ProductIds = items.Select(i => i.ProductId).ToList() }, transaction);

        await transaction.CommitAsync();

        Log.Information("PESANAN {Nomor} dibuat | OrderId={OrderId} | Pelanggan={Caller} | Subtotal={Subtotal} | Ongkir={Shipping} | Pajak={Tax} | Total={Total} | By={By}",
            nomorPesanan, idPesanan, createdBy, subtotal, shipping, taxAmount, total, createdBy);

        var (order, _) = await GetByIdAsync(idPesanan);
        return (order, null);
    }

    /// <summary>Buat nomor pesanan INV{yyyyMMddHHmmssfff}{urutan 5 digit}; panggil dalam transaksi (retry tabrakan di pemanggil).</summary>
    private static async Task<string> BangunNomorPesananAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction)
    {
        var basis = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var sudah = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM LOSCONSUMER.TRX_ORDER WHERE ORDER_NUMBER LIKE @Basis + '%';",
            new { Basis = "INV" + basis }, transaction);
        return "INV" + basis + (sudah + 1).ToString("D5");
    }

    public async Task<(OrderDetailDto? Order, string? Error)> GetByIdAsync(long id)
    {
        using var connection = new SqlConnection(_connectionString);
        var order = await connection.QueryFirstOrDefaultAsync<dynamic>("""
            SELECT O.*, C.EMAIL AS CUSTOMER_EMAIL, ISNULL(C.NAME, C.EMAIL) AS CUSTOMER_NAME
            FROM LOSCONSUMER.TRX_ORDER O
            JOIN LOSCONSUMER.MASTER_CUSTOMER C ON C.ID = O.CUSTOMER_ID
            WHERE O.ID = @Id;
            """, new { Id = id });

        if (order is null)
        {
            return (null, null);
        }

        var items = (await connection.QueryAsync<OrderItemDto>("""
            SELECT ID, ORDER_ID, PRODUCT_ID, TITLE, UNIT_PRICE, QUANTITY, SUBTOTAL
            FROM LOSCONSUMER.TRX_ORDER_ITEM
            WHERE ORDER_ID = @OrderId
            ORDER BY ID;
            """, new { OrderId = id })).ToList();

        return (ToDetailDto(order, items), null);
    }

    public async Task<List<OrderDto>> GetByCustomerAsync(int customerId)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<dynamic>("""
            SELECT *
            FROM LOSCONSUMER.TRX_ORDER
            WHERE CUSTOMER_ID = @CustomerId
            ORDER BY ID DESC;
            """, new { CustomerId = customerId });
        return rows.Select(ToDto).ToList();
    }

    public async Task<PagedResult<OrderDto>> GetPagedAsync(OrderQueryParams query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            conditions.Add("O.STATUS = @Status");
            parameters.Add("Status", query.Status.Trim().ToUpperInvariant());
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("""
                (C.EMAIL LIKE @Search ESCAPE '\'
                 OR C.NAME LIKE @Search ESCAPE '\'
                 OR CAST(O.TOTAL_AMOUNT AS NVARCHAR(50)) LIKE @Search ESCAPE '\')
                """);
            parameters.Add("Search", $"%{EscapeLike(query.Search.Trim())}%");
        }

        var whereClause = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        var sortColumn = !string.IsNullOrEmpty(query.SortBy)
            && SortColumns.TryGetValue(query.SortBy, out var column) ? column : "O.CREATED_AT";
        var sortOrder = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var tieBreaker = sortColumn == "O.ID" ? string.Empty : ", O.ID DESC";
        var offset = (page - 1) * pageSize;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        using var connection = new SqlConnection(_connectionString);

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)
            FROM LOSCONSUMER.TRX_ORDER O
            JOIN LOSCONSUMER.MASTER_CUSTOMER C ON C.ID = O.CUSTOMER_ID
            {whereClause};
            """, parameters);

        var rows = await connection.QueryAsync<dynamic>($"""
            SELECT O.*, C.EMAIL AS CUSTOMER_EMAIL, ISNULL(C.NAME, C.EMAIL) AS CUSTOMER_NAME
            FROM LOSCONSUMER.TRX_ORDER O
            JOIN LOSCONSUMER.MASTER_CUSTOMER C ON C.ID = O.CUSTOMER_ID
            {whereClause}
            ORDER BY {sortColumn} {sortOrder}{tieBreaker}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters);

        return new PagedResult<OrderDto>
        {
            Items = rows.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<(bool Success, string? Error)> PackAsync(long id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.TRX_ORDER
            SET    STATUS     = 'DIKEMAS',
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID = @Id AND STATUS = 'MENUNGGU_KONFIRMASI';
            """, new { Id = id, UpdatedBy = updatedBy });

        if (affected > 0)
        {
            await _audit.LogAsync("ORDER", id.ToString(), "ORDER_PACKED", null, null, $"Dikemas oleh {updatedBy}");
        }
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> ShipAsync(long id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.TRX_ORDER
            SET    STATUS     = 'DIKIRIM',
                   KIRIM_AT   = GETDATE(),
                   KIRIM_BY   = @UpdatedBy,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID = @Id AND STATUS = 'DIKEMAS';
            """, new { Id = id, UpdatedBy = updatedBy });

        if (affected > 0)
        {
            await _audit.LogAsync("ORDER", id.ToString(), "ORDER_SHIPPED", null, null, $"Dikirim oleh {updatedBy}");
        }
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> CancelAsync(
        long id, string reason, string updatedBy, bool fromPenjual = false)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, "Alasan pembatalan wajib diisi.");
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        var current = await connection.ExecuteScalarAsync<string?>("""
            SELECT STATUS FROM LOSCONSUMER.TRX_ORDER WHERE ID = @Id;
            """, new { Id = id }, transaction);

        if (current is null)
        {
            return (false, "Pesanan tidak ditemukan.");
        }
        if (current == MenungguKonfirmasi)
        {
            // Pembeli maupun penjual dapat membatalkan sebelum konfirmasi penjual.
        }
        else if (current == Dikemas)
        {
            if (!fromPenjual)
            {
                return (false, "Pesanan sudah dikemas, hanya penjual yang dapat membatalkan.");
            }
        }
        else
        {
            return current == Dikirim
                ? (false, "Pesanan sudah dikirim — tidak dapat dibatalkan.")
                : (false, $"Pesanan berstatus {current} — tidak dapat dibatalkan.");
        }

        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.TRX_ORDER
            SET    STATUS       = 'DIBATALKAN',
                   BATAL_AT     = GETDATE(),
                   BATAL_BY     = @UpdatedBy,
                   BATAL_REASON = @Reason,
                   UPDATED_AT   = GETDATE(),
                   UPDATED_BY   = @UpdatedBy,
                   VERSION      = VERSION + 1
            WHERE  ID = @Id;
            """, new { Id = id, UpdatedBy = updatedBy, Reason = reason.Trim() }, transaction);

        if (affected > 0)
        {
            // Stok kembali saat DIBATALKAN dari MENUNGGU_KONFIRMASI atau DIKEMAS (barang belum dikirim).
            var items = (await connection.QueryAsync<dynamic>("""
                SELECT PRODUCT_ID, QUANTITY FROM LOSCONSUMER.TRX_ORDER_ITEM WHERE ORDER_ID = @OrderId;
                """, new { OrderId = id }, transaction)).ToList();

            foreach (var item in items)
            {
                await connection.ExecuteAsync("""
                    UPDATE LOSCONSUMER.MASTER_PRODUCT
                    SET STOCK = STOCK + @Quantity
                    WHERE ID = @Id;
                    """, new { Quantity = (int)item.QUANTITY, Id = (int)item.PRODUCT_ID }, transaction);
            }
        }

        await transaction.CommitAsync();

        if (affected > 0)
        {
            await _audit.LogAsync("ORDER", id.ToString(), "ORDER_CANCELLED", null, null, $"{updatedBy}: {reason.Trim()}");
        }
        return (affected > 0, null);
    }

    public async Task<(bool Success, string? Error)> ReceiveAsync(long id, string updatedBy)
    {
        using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.TRX_ORDER
            SET    STATUS     = 'DITERIMA',
                   TERIMA_AT  = GETDATE(),
                   TERIMA_BY  = @UpdatedBy,
                   UPDATED_AT = GETDATE(),
                   UPDATED_BY = @UpdatedBy,
                   VERSION    = VERSION + 1
            WHERE  ID = @Id AND STATUS = 'DIKIRIM';
            """, new { Id = id, UpdatedBy = updatedBy });

        return (affected > 0, null);
    }

    public async Task<SalesReportDto> GetSalesReportAsync(int? days)
    {
        var label = days switch
        {
            7 => "7 Hari Terakhir",
            30 => "30 Hari Terakhir",
            _ => "Sepanjang Masa"
        };

        var from = days.HasValue ? DateTime.Now.AddDays(-days.Value) : (DateTime?)null;

        using var connection = new SqlConnection(_connectionString);
        var top = (await connection.QueryAsync<SalesRowDto>("""
            SELECT TOP 10
                I.PRODUCT_ID AS ProductId,
                MAX(I.TITLE) AS Title,
                SUM(I.QUANTITY) AS TotalQuantity,
                SUM(I.SUBTOTAL) AS Revenue
            FROM LOSCONSUMER.TRX_ORDER_ITEM I
            JOIN LOSCONSUMER.TRX_ORDER O ON O.ID = I.ORDER_ID
            WHERE O.STATUS <> 'DIBATALKAN'
              AND (@From IS NULL OR O.CREATED_AT >= @From)
            GROUP BY I.PRODUCT_ID
            ORDER BY SUM(I.QUANTITY) DESC;
            """, new { From = from })).ToList();

        var bottom = (await connection.QueryAsync<SalesRowDto>("""
            SELECT TOP 10
                I.PRODUCT_ID AS ProductId,
                MAX(I.TITLE) AS Title,
                SUM(I.QUANTITY) AS TotalQuantity,
                SUM(I.SUBTOTAL) AS Revenue
            FROM LOSCONSUMER.TRX_ORDER_ITEM I
            JOIN LOSCONSUMER.TRX_ORDER O ON O.ID = I.ORDER_ID
            WHERE O.STATUS <> 'DIBATALKAN'
              AND (@From IS NULL OR O.CREATED_AT >= @From)
            GROUP BY I.PRODUCT_ID
            ORDER BY SUM(I.QUANTITY) ASC;
            """, new { From = from })).ToList();

        return new SalesReportDto { Label = label, Top = top, Bottom = bottom };
    }

    public async Task<(int Pending, int Today)> GetDashboardOrderStatsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        var stats = await connection.QuerySingleAsync<dynamic>("""
            SELECT
                (SELECT COUNT(*) FROM LOSCONSUMER.TRX_ORDER WHERE STATUS IN ('MENUNGGU_KONFIRMASI', 'DIKEMAS')) AS Pending,
                (SELECT COUNT(*) FROM LOSCONSUMER.TRX_ORDER WHERE CAST(CREATED_AT AS DATE) = CAST(GETDATE() AS DATE)) AS Today;
            """);
        return ((int)stats.Pending, (int)stats.Today);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static OrderDto ToDto(dynamic row) => new()
    {
        Id = (long)row.ID,
        CustomerId = (int)row.CUSTOMER_ID,
        CustomerEmail = (string)row.CUSTOMER_EMAIL,
        CustomerName = (string)row.CUSTOMER_NAME,
        Status = (string)row.STATUS,
        Subtotal = (decimal)row.SUBTOTAL,
        ShippingFee = (decimal)row.SHIPPING_FEE,
        TaxAmount = (decimal)row.TAX_AMOUNT,
        TotalAmount = (decimal)row.TOTAL_AMOUNT,
        CourierId = row.COURIER_ID as int?,
        CourierName = row.COURIER_NAME as string,
        ShipName = row.SHIP_NAME as string,
        ShipPhone = row.SHIP_PHONE as string,
        ShipAddress = row.SHIP_ADDRESS as string,
        Note = row.NOTE as string,
        OrderNumber = row.ORDER_NUMBER as string,
        CreatedAt = (DateTime)row.CREATED_AT,
        DiprosesAt = (DateTime)row.DIPROSES_AT,
        KirimAt = row.KIRIM_AT as DateTime?,
        KirimBy = row.KIRIM_BY as string,
        TerimaAt = row.TERIMA_AT as DateTime?,
        TerimaBy = row.TERIMA_BY as string,
        BatalAt = row.BATAL_AT as DateTime?,
        BatalBy = row.BATAL_BY as string,
        BatalReason = row.BATAL_REASON as string,
        Version = (int)row.VERSION
    };

    private static OrderDetailDto ToDetailDto(dynamic row, List<OrderItemDto> items) => new()
    {
        Id = (long)row.ID,
        CustomerId = (int)row.CUSTOMER_ID,
        CustomerEmail = (string)row.CUSTOMER_EMAIL,
        CustomerName = (string)row.CUSTOMER_NAME,
        Status = (string)row.STATUS,
        Subtotal = (decimal)row.SUBTOTAL,
        ShippingFee = (decimal)row.SHIPPING_FEE,
        TaxAmount = (decimal)row.TAX_AMOUNT,
        TotalAmount = (decimal)row.TOTAL_AMOUNT,
        CourierId = row.COURIER_ID as int?,
        CourierName = row.COURIER_NAME as string,
        ShipName = row.SHIP_NAME as string,
        ShipPhone = row.SHIP_PHONE as string,
        ShipAddress = row.SHIP_ADDRESS as string,
        Note = row.NOTE as string,
        OrderNumber = row.ORDER_NUMBER as string,
        CreatedAt = (DateTime)row.CREATED_AT,
        DiprosesAt = (DateTime)row.DIPROSES_AT,
        KirimAt = row.KIRIM_AT as DateTime?,
        KirimBy = row.KIRIM_BY as string,
        TerimaAt = row.TERIMA_AT as DateTime?,
        TerimaBy = row.TERIMA_BY as string,
        BatalAt = row.BATAL_AT as DateTime?,
        BatalBy = row.BATAL_BY as string,
        BatalReason = row.BATAL_REASON as string,
        Version = (int)row.VERSION,
        Items = items
    };
}