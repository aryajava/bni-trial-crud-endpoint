using cobaproject.Models;

namespace cobaproject.Services.Interfaces;

public interface IResponseLogService
{
    Task<long> InsertAsync(ResponseProduct response);
}
