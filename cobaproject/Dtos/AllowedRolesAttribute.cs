using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

[AttributeUsage(AttributeTargets.Property)]
public sealed class AllowedRolesAttribute : ValidationAttribute
{
    public AllowedRolesAttribute() : base("Role yang dipilih tidak dikenali.")
    {
    }

    public override bool IsValid(object? value) =>
        value is string role && (role == "ADMIN" || role == "OWNER");
}