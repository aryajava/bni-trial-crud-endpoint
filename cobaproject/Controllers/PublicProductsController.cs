using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/products/public")]
public class PublicProductsController : ControllerBase
{
    private readonly IFakeStoreService _fakeStoreService;

    public PublicProductsController(IFakeStoreService fakeStoreService)
    {
        _fakeStoreService = fakeStoreService;
    }

    [HttpGet("insert")]
    public async Task<IResult> Insert()
    {
        try
        {
            var (inserted, skipped) = await _fakeStoreService.InsertFromFakeStoreAsync();
            return ResponseHelper.Success(HttpContext,
                new { inserted, skipped },
                "Data dari FakeStore berhasil disinkronkan.");
        }
        catch
        {
            return ResponseHelper.BadGateway(HttpContext);
        }
    }

    [HttpGet]
    public async Task<IResult> GetAll()
    {
        try
        {
            var products = await _fakeStoreService.GetAllAsync();
            return ResponseHelper.Success(HttpContext, products.ToList());
        }
        catch
        {
            return ResponseHelper.BadGateway(HttpContext);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IResult> GetById(int id)
    {
        try
        {
            var product = await _fakeStoreService.GetByIdAsync(id);
            return product is null
                ? ResponseHelper.NotFound(HttpContext)
                : ResponseHelper.Success(HttpContext, product);
        }
        catch
        {
            return ResponseHelper.BadGateway(HttpContext);
        }
    }
}