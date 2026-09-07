using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Panel;

[Authorize]
public class NotifikasiModel : PageModel
{
    private readonly IAuditLogService _auditLogService;
    private readonly IUserService _userService;

    public List<OrderNotificationDto> Items { get; set; } = [];
    public int Unread { get; set; }

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public NotifikasiModel(IAuditLogService auditLogService, IUserService userService)
    {
        _auditLogService = auditLogService;
        _userService = userService;
    }

    public async Task OnGetAsync()
    {
        var readAt = await _userService.GetNotifReadAtAsync(CurrentUserId);
        Items = await _auditLogService.GetOrderNotificationsAsync(readAt);
        Unread = Items.Count;
        ViewData["Title"] = "Notifikasi";
    }

    /// <summary>Ringkasan untuk badge & toast (dipanggil polling layout tiap 10 detik).</summary>
    public async Task<JsonResult> OnGetSummary()
    {
        var readAt = await _userService.GetNotifReadAtAsync(CurrentUserId);
        var unread = await _auditLogService.CountOrderNotificationsAsync(readAt);
        var lates = await _auditLogService.GetOrderNotificationsAsync(readAt, 1);
        return new JsonResult(new
        {
            unread,
            latest = lates.FirstOrDefault() is { } terbaru ? new
            {
                terbaru.OrderNumber,
                label = terbaru.Action == "ORDER_CANCELLED" ? "DIBATALKAN" : "DITERIMA",
                terbaru.Actor,
                waktu = terbaru.ActedAt.ToString("HH:mm")
            } : null
        });
    }

    public async Task<IActionResult> OnPostMarkReadAsync()
    {
        await _userService.SetNotifReadAtAsync(CurrentUserId);
        TempData["InfoMessage"] = "Semua notifikasi ditandai sudah dibaca.";
        return RedirectToPage("/Panel/Notifikasi");
    }
}