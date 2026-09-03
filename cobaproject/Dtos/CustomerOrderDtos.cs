namespace cobaproject.Dtos;

public class CustomerDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public int Version { get; set; }

    public string Display => !string.IsNullOrWhiteSpace(Name) ? Name! : Email;
}

public class CustomerQueryParams : PageRequest
{
    [System.ComponentModel.Description("Filter status: true = aktif, false = nonaktif (kosong = semua).")]
    public bool? Active { get; set; }

    [System.ComponentModel.Description("Filter status blokir: true = diblokir, false = tidak (kosong = semua).")]
    public bool? Blocked { get; set; }
}

public class RegisterCustomerRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email wajib diisi.")]
    [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Format email tidak valid.")]
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "Email maksimal 200 karakter.")]
    public string Email { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Kata sandi wajib diisi.")]
    [System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 6, ErrorMessage = "Kata sandi minimal 6 karakter.")]
    public string Password { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Nama wajib diisi.")]
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "Nama maksimal 200 karakter.")]
    public string Name { get; set; } = string.Empty;
}

public class UpdateCustomerProfileRequest
{
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "Nama maksimal 200 karakter.")]
    public string? Name { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(50, ErrorMessage = "No. HP maksimal 50 karakter.")]
    public string? Phone { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(2000, ErrorMessage = "Alamat maksimal 2000 karakter.")]
    public string? Address { get; set; }
}

public class ResetCustomerPasswordRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Kata sandi baru wajib diisi.")]
    [System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 6, ErrorMessage = "Kata sandi minimal 6 karakter.")]
    public string NewPassword { get; set; } = string.Empty;
}

public class CustomerAuditEntryDto
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime ActedAt { get; set; }
    public string? Reason { get; set; }
}

public class CartItemDto
{
    public int ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal EffectivePrice { get; set; }
    public int Stock { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal => EffectivePrice * Quantity;
}

public class OrderItemDto
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public int ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
}

public class OrderDto
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = "DIPROSES";
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ShipName { get; set; }
    public string? ShipPhone { get; set; }
    public string? ShipAddress { get; set; }
    public string? Note { get; set; }
    public DateTime DiprosesAt { get; set; }
    public DateTime? KirimAt { get; set; }
    public string? KirimBy { get; set; }
    public DateTime? TerimaAt { get; set; }
    public string? TerimaBy { get; set; }
    public DateTime? BatalAt { get; set; }
    public string? BatalBy { get; set; }
    public string? BatalReason { get; set; }
    public int Version { get; set; }
}

public class OrderDetailDto : OrderDto
{
    public List<OrderItemDto> Items { get; set; } = [];
}

public class OrderQueryParams : PageRequest
{
    [System.ComponentModel.Description("Filter status: DIPROSES, DIKIRIM, DITERIMA, DIBATALKAN (kosong = semua).")]
    public string? Status { get; set; }
}

public class CheckoutRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Nama penerima wajib diisi.")]
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "Nama penerima maksimal 200 karakter.")]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(50, ErrorMessage = "No. HP maksimal 50 karakter.")]
    public string? Phone { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Alamat pengiriman wajib diisi.")]
    [System.ComponentModel.DataAnnotations.StringLength(2000, ErrorMessage = "Alamat maksimal 2000 karakter.")]
    public string Address { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(500, ErrorMessage = "Catatan maksimal 500 karakter.")]
    public string? Note { get; set; }
}

public class CancelOrderRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Alasan pembatalan wajib diisi.")]
    [System.ComponentModel.DataAnnotations.StringLength(500, ErrorMessage = "Alasan maksimal 500 karakter.")]
    public string Reason { get; set; } = string.Empty;
}

public class AuditLogQueryParams : PageRequest
{
    [System.ComponentModel.Description("Filter entitas (PRODUCT, USER, ORDER, SETTING, ...).")]
    public string? Entity { get; set; }

    [System.ComponentModel.Description("Filter aksi (CREATE, UPDATE, APPROVE, ...).")]
    public string? Action { get; set; }

    [System.ComponentModel.Description("Batas bawah rentang waktu (inclusive).")]
    public DateTime? From { get; set; }

    [System.ComponentModel.Description("Batas atas rentang waktu (inclusive).")]
    public DateTime? To { get; set; }
}

public class AuditLogEntryDto
{
    public long Id { get; set; }
    public string Entity { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime ActedAt { get; set; }
    public string? OldSnapshot { get; set; }
    public string? NewSnapshot { get; set; }
    public string? Reason { get; set; }
    public string? TraceId { get; set; }
}

public class SalesReportDto
{
    public string Label { get; set; } = string.Empty;
    public List<SalesRowDto> Top { get; set; } = [];
    public List<SalesRowDto> Bottom { get; set; } = [];
}

public class SalesRowDto
{
    public int ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal Revenue { get; set; }
}