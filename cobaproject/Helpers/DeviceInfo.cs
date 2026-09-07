namespace cobaproject.Helpers;

/// <summary>
/// Identitas perangkat untuk Sesi Aktif. Label tersimpan: "Browser | OS | IP";
/// pembandingan "perangkat yang sama" hanya memakai dua segmen pertama (browser|OS)
/// agar IP dinamis tidak memicu tolak palsu.
/// </summary>
public static class DeviceInfo
{
    public static string BuatLabel(string? userAgent, string? ip)
    {
        var ua = userAgent ?? string.Empty;
        var browser = ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge"
            : ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ? "Chrome"
            : ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox"
            : ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase) ? "Safari"
            : ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase) ? "Opera"
            : "Peramban lain";
        var os = ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows"
            : ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android"
            : ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iOS"
            : ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) ? "macOS"
            : ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux"
            : "OS lain";
        var label = $"{browser} | {os}";
        return string.IsNullOrWhiteSpace(ip) ? label : $"{label} | {ip}";
    }

    /// <summary>Perangkat sama bila dua segmen pertama (browser|OS) sama; IP diabaikan.</summary>
    public static bool Sesuai(string? tersimpan, string? saatIni)
    {
        if (string.IsNullOrWhiteSpace(tersimpan) || string.IsNullOrWhiteSpace(saatIni))
        {
            return false;
        }
        var a = tersimpan.Split('|');
        var b = saatIni.Split('|');
        return a.Length >= 2 && b.Length >= 2
            && a[0].Trim().Equals(b[0].Trim(), StringComparison.OrdinalIgnoreCase)
            && a[1].Trim().Equals(b[1].Trim(), StringComparison.OrdinalIgnoreCase);
    }
}