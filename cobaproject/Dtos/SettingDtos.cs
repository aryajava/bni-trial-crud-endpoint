namespace cobaproject.Dtos;

public class SettingDto
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public int Version { get; set; }
}

public class UpdateSettingRequest
{
    public string Value { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue,
        ErrorMessage = "Versi tidak valid.")]
    public int Version { get; set; }
}