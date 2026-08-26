using System.Net.Http.Json;
using Dapper;
using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Mappers;
using cobaproject.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class FakeStoreService : IFakeStoreService
{
    private readonly HttpClient _httpClient;
    private readonly string _connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<FakeStoreService> _logger;

    public FakeStoreService(
        HttpClient httpClient,
        IOptions<DatabaseConfig> config,
        IHttpContextAccessor httpContextAccessor,
        ILogger<FakeStoreService> logger)
    {
        _httpClient = httpClient;
        _connectionString = config.Value.DefaultConnection;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private string TraceId =>
        _httpContextAccessor.HttpContext?.Items["TraceId"]?.ToString() ?? Guid.NewGuid().ToString();

    public async Task<IEnumerable<FakeStoreProductDto>> GetAllAsync()
    {
        var response = await _httpClient.GetAsync("products");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<FakeStoreProductDto>>() ?? [];
    }

    public async Task<FakeStoreProductDto?> GetByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"products/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FakeStoreProductDto>();
    }

    public async Task<(int Inserted, int Skipped)> InsertFromFakeStoreAsync()
    {
        var products = (await GetAllAsync()).ToList();

        using var connection = new SqlConnection(_connectionString);

        // Ambil existing IDs dari database untuk cek duplikasi
        var existingIds = (await connection.QueryAsync<int>(
            "SELECT ID FROM LOSCONSUMER.MASTER_PRODUCT;"))
            .ToHashSet();

        int inserted = 0, skipped = 0;
        foreach (var product in products)
        {
            if (existingIds.Contains(product.Id))
            {
                skipped++;
                continue;
            }

            var entity = ProductMapper.ToEntity(product);
            var sql = """
                INSERT INTO LOSCONSUMER.MASTER_PRODUCT
                    (TITLE, PRICE, DESCRIPTION, CATEGORY, IMAGE,
                     RATING_RATE, RATING_COUNT, IS_ACTIVE, CREATED_AT, CREATED_BY,
                     UPDATED_AT, UPDATED_BY, VERSION)
                VALUES
                    (@Title, @Price, @Description, @Category, @Image,
                     @RatingRate, @RatingCount, 1, GETDATE(), @CreatedBy,
                     NULL, NULL, 1);
                """;
            await connection.ExecuteAsync(sql, entity);
            inserted++;
        }

        _logger.LogInformation(
            "[MASTER_PRODUCT] INSERT_FROM_FAKESTORE | Inserted={Inserted} | Skipped={Skipped} | TraceId={TraceId}",
            inserted, skipped, TraceId);

        return (inserted, skipped);
    }
}
