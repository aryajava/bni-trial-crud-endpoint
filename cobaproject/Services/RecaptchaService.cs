using System.Net.Http.Json;
using cobaproject.Services.Interfaces;

namespace cobaproject.Services;

public class RecaptchaService : IRecaptchaService
{
    private const string SiteVerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    private readonly ISettingService _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RecaptchaService> _logger;

    public RecaptchaService(
        ISettingService settings,
        IHttpClientFactory httpClientFactory,
        ILogger<RecaptchaService> logger)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(bool Active, bool Passed, string? Error)> VerifyAsync(string? token, string? remoteIp)
    {
        var siteKey = (await _settings.GetAsync(SettingService.RecaptchaSiteKey))?.Value.Trim();
        var secret = (await _settings.GetAsync(SettingService.RecaptchaSecretKey))?.Value.Trim();
        if (string.IsNullOrWhiteSpace(siteKey) || string.IsNullOrWhiteSpace(secret))
        {
            return (false, true, null);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return (true, false, "Centang \"Saya bukan robot\" untuk melanjutkan.");
        }

        var form = new Dictionary<string, string>
        {
            ["secret"] = secret,
            ["response"] = token.Trim()
        };
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            form["remoteip"] = remoteIp.Trim();
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var response = await client.PostAsync(SiteVerifyUrl, new FormUrlEncodedContent(form));
            var hasil = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>();
            if (hasil is { Success: true })
            {
                return (true, true, null);
            }

            _logger.LogWarning("[RECAPTCHA] Verifikasi ditolak Google | RemoteIp={RemoteIp}", remoteIp);
            return (true, false, "Verifikasi reCAPTCHA ditolak. Centang ulang, lalu coba lagi.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RECAPTCHA] Gagal memanggil siteverify");
            return (true, false, "Layanan verifikasi reCAPTCHA tidak dapat dijangkau. Coba lagi.");
        }
    }

    private sealed class SiteVerifyResponse
    {
        public bool Success { get; set; }
    }
}