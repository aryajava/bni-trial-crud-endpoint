using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace cobaproject.Helpers;

/// <summary>
/// ALTCHA proof-of-work v2, sepenuhnya self-hosted (tanpa panggilan keluar).
/// Server membuat challenge {algorithm, challenge, salt, signature}; browser
/// memecahkannya dengan mencari number yang menghasilkan hash yang sama;
/// server memverifikasi dengan satu perhitungan KDF + HMAC.
/// </summary>
public static class Altcha
{
    public const string AlgorithmSha256 = "SHA-256";
    private const string HashAlg = "SHA256";
    private const string HmacAlg = "HMACSHA256";

    /// <summary>Rentang number yang harus dicoba browser (makin lebar makin lama solve).</summary>
    public static (int Min, int Max) NumberRange { get; set; } = (50_000, 100_000);

    /// <summary>Umur challenge sebelum dianggap kedaluwarsa.</summary>
    public static TimeSpan Expiry { get; set; } = TimeSpan.FromSeconds(120);

    private sealed class Payload
    {
        public string? Algorithm { get; set; }
        public string? Challenge { get; set; }
        public string? Salt { get; set; }
        public string? Signature { get; set; }
        public long? Number { get; set; }
    }

    /// <summary>Buat JSON challenge untuk widget. Kembali null bila kunci tidak valid.</summary>
    public static string? BuatChallenge(string hmacKey)
    {
        if (string.IsNullOrWhiteSpace(hmacKey) || hmacKey.Length < 16)
        {
            return null;
        }

        var salt = GenerateHex(24) + "?expires=" +
            DateTimeOffset.UtcNow.Add(Expiry).ToUnixTimeSeconds();
        var number = RandomNumberGenerator.GetInt32(NumberRange.Min, NumberRange.Max);
        var challenge = HashHex(salt + number);

        return JsonSerializer.Serialize(new
        {
            algorithm = AlgorithmSha256,
            challenge,
            salt,
            signature = HmacHex(hmacKey, challenge)
        });
    }

    /// <summary>Verifikasi payload (base64url JSON) yang dikirim widget.</summary>
    public static bool Verify(string hmacKey, string? payload, Action<string>? onGagal = null)
    {
        if (string.IsNullOrWhiteSpace(hmacKey) || hmacKey.Length < 16 ||
            string.IsNullOrWhiteSpace(payload))
        {
            onGagal?.Invoke("kunci tidak valid atau payload kosong");
            return false;
        }

        try
        {
            var p = JsonSerializer.Deserialize<Payload>(Base64UrlDecode(payload!));
            if (p is null || p.Number is null || string.IsNullOrWhiteSpace(p.Algorithm) ||
                string.IsNullOrWhiteSpace(p.Challenge) || string.IsNullOrWhiteSpace(p.Salt) ||
                string.IsNullOrWhiteSpace(p.Signature))
            {
                onGagal?.Invoke("payload tidak lengkap");
                return false;
            }

            var parts = p.Salt.Split(new[] { '?', '/' }, 2);
            if (parts.Length != 2 || !parts[1].StartsWith("expires=", StringComparison.Ordinal))
            {
                onGagal?.Invoke("salt tanpa expires");
                return false;
            }
            if (!long.TryParse(parts[1]["expires=".Length..], out var expires) ||
                DateTimeOffset.FromUnixTimeSeconds(expires) < DateTimeOffset.UtcNow)
            {
                onGagal?.Invoke($"payload kedaluwarsa (expires={parts[1]["expires=".Length..]})");
                return false;
            }

            if (!string.Equals(p.Algorithm, AlgorithmSha256, StringComparison.OrdinalIgnoreCase))
            {
                onGagal?.Invoke("algoritma tidak dikenali");
                return false;
            }

            var ulang = HashHex(p.Salt + p.Number);
            var saltNormal = p.Salt.Replace("/expires=", "?expires=", StringComparison.OrdinalIgnoreCase);
            var cocok = string.Equals(ulang, p.Challenge, StringComparison.OrdinalIgnoreCase);
            if (!cocok && !string.Equals(saltNormal, p.Salt, StringComparison.Ordinal))
            {
                cocok = string.Equals(HashHex(saltNormal + p.Number), p.Challenge, StringComparison.OrdinalIgnoreCase);
            }
            if (!cocok)
            {
                onGagal?.Invoke("rehash tidak cocok dengan challenge");
                return false;
            }

            var tandaTangan = HmacHex(hmacKey, p.Challenge);
            if (!string.Equals(tandaTangan, p.Signature, StringComparison.OrdinalIgnoreCase))
            {
                onGagal?.Invoke("tanda tangan tidak cocok");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            onGagal?.Invoke("pengecualian: " + ex.Message);
            return false;
        }
    }

    private static string HashHex(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string HmacHex(string key, string input)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GenerateHex(int byteLength) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength)).ToLowerInvariant();

    private static string Base64UrlDecode(string input)
    {
        var normal = input.Replace('-', '+').Replace('_', '/');
        normal = normal.PadRight(normal.Length + (4 - normal.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(normal));
    }
}