using Dapper;
using cobaproject.Configuration;
using cobaproject.Models;
using cobaproject.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class RequestLogService : IRequestLogService
{
    private readonly string _connectionString;

    public RequestLogService(IOptions<DatabaseConfig> config)
    {
        _connectionString = config.Value.DefaultConnection;
    }

    public async Task<long> InsertAsync(RequestProduct request)
    {
        using var connection = new SqlConnection(_connectionString);
        var sql = """
            INSERT INTO LOSCONSUMER.REQUEST_PRODUCT
                (TRACE_ID, ENDPOINT, HTTP_METHOD, HEADERS, QUERY_PARAMS, BODY, IP_ADDRESS, REQUESTED_AT)
            OUTPUT INSERTED.ID
            VALUES
                (@TraceId, @Endpoint, @HttpMethod, @Headers, @QueryParams, @Body, @IpAddress, GETDATE());
            """;
        return await connection.ExecuteScalarAsync<long>(sql, request);
    }
}
