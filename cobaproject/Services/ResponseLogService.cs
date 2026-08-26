using Dapper;
using cobaproject.Configuration;
using cobaproject.Models;
using cobaproject.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class ResponseLogService : IResponseLogService
{
    private readonly string _connectionString;

    public ResponseLogService(IOptions<DatabaseConfig> config)
    {
        _connectionString = config.Value.DefaultConnection;
    }

    public async Task<long> InsertAsync(ResponseProduct response)
    {
        using var connection = new SqlConnection(_connectionString);
        var sql = """
            INSERT INTO LOSCONSUMER.RESPONSE_PRODUCT
                (TRACE_ID, STATUS_CODE, IS_SUCCESS, MESSAGE, RESPONSE_BODY, ELAPSED_MS, RESPONDED_AT)
            OUTPUT INSERTED.ID
            VALUES
                (@TraceId, @StatusCode, @IsSuccess, @Message, @ResponseBody, @ElapsedMs, GETDATE());
            """;
        return await connection.ExecuteScalarAsync<long>(sql, response);
    }
}
