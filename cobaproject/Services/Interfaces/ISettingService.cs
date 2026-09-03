using cobaproject.Dtos;

namespace cobaproject.Services.Interfaces;

public interface ISettingService
{
    Task<SettingDto?> GetAsync(string key);

    Task<(bool Success, string? Error)> UpdateAsync(string key, string value, int version, string updatedBy);
}