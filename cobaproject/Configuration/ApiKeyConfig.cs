namespace cobaproject.Configuration;

public class ApiKeyConfig
{
    public string HeaderName { get; set; } = "X-Api-Key";
    public string Key { get; set; } = string.Empty;
}