namespace cobaproject.Dtos;

public class DashboardStatsDto
{
    public int Total { get; set; }

    public int Discounted { get; set; }

    public int LowStock { get; set; }

    public int OutOfStock { get; set; }
}