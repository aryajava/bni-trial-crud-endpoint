namespace cobaproject.Helpers;

public static class UserRolePolicy
{
    public const string Sa = "SA";
    public const string Admin = "ADMIN";
    public const string Owner = "OWNER";

    public static int Rank(string role) => role switch
    {
        Sa => 3,
        Owner => 2,
        _ => 1
    };

    public static bool IsValidRole(string? role) => role is Sa or Owner or Admin;

    /// <summary>
    /// Label tampilan untuk user (badge, dropdown, pesan). Nilai internal tetap
    /// SA/OWNER/ADMIN; istilah "manusia" hanya di lapisan tampilan.
    /// </summary>
    public static string DisplayName(string role) => role switch
    {
        Sa => "Super Admin",
        Owner => "Pemilik Toko",
        Admin => "Admin Toko",
        _ => role
    };

    /// <summary>
    /// Apakah <paramref name="actorRole"/> berhak mengelola akun ber-role <paramref name="targetRole"/>.
    /// Berdasarkan rank: SA &gt; OWNER &gt; ADMIN.
    /// </summary>
    public static bool CanManage(string actorRole, string targetRole) =>
        Rank(actorRole) >= Rank(targetRole);
}