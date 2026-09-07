namespace cobaproject.Services.Interfaces;

public interface IRecaptchaService
{
    /// <summary>Verifikasi token reCAPTCHA ke Google. Active=false berarti kunci belum diatur (verifikasi dilewati).</summary>
    Task<(bool Active, bool Passed, string? Error)> VerifyAsync(string? token, string? remoteIp);
}