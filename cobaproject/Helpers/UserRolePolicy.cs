namespace cobaproject.Helpers;

public static class UserRolePolicy
{
    public const string Admin = "ADMIN";
    public const string Owner = "OWNER";

    public static int Rank(string role) => role switch
    {
        Owner => 2,
        _ => 1
    };

    public static bool IsValidRole(string? role) => role is Admin or Owner;

    /// <summary>
    /// Label tampilan untuk user (badge, dropdown, pesan). Nilai internal tetap
    /// OWNER/ADMIN; istilah "manusia" hanya di lapisan tampilan.
    /// </summary>
    public static string DisplayName(string role) => role switch
    {
        Owner => "Pemilik Toko",
        Admin => "Admin Toko",
        _ => role
    };

    /// <summary>
    /// Apakah <paramref name="actorRole"/> berhak mengelola akun ber-role <paramref name="targetRole"/>.
    /// Owner boleh mengelola semua; Admin hanya sesama Admin (hierarki Owner &gt; Admin).
    /// </summary>
    public static bool CanManage(string actorRole, string targetRole) =>
        Rank(actorRole) >= Rank(targetRole);
}