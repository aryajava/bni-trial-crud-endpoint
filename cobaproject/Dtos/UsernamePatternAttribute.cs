using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace cobaproject.Dtos;

[AttributeUsage(AttributeTargets.Property)]
public sealed class UsernamePatternAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is not string s || s.Length == 0 || Regex.IsMatch(s, "^[a-z0-9]+$");
}