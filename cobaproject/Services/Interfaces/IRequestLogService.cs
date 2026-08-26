using cobaproject.Models;

namespace cobaproject.Services.Interfaces;

public interface IRequestLogService
{
    Task<long> InsertAsync(RequestProduct request);
}
