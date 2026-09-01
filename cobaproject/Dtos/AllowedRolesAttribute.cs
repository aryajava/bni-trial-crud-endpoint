using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

[AttributeUsage(AttributeTargets.Property)]
public sealed class AllowedRolesAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string role && (role == "ADMIN" || role == "OWNER");
}