using System.Diagnostics;
using System.Text;
using System.Text.Json;
using cobaproject.Models;
using cobaproject.Services.Interfaces;

namespace cobaproject.Helpers;

public class RequestResponseMiddleware
{
    private static readonly string[] ExcludedPathPrefixes = ["/swagger", "/openapi", "/favicon.ico", "/_framework", "/_vs"];

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseMiddleware> _logger;

    public RequestResponseMiddleware(RequestDelegate next, ILogger<RequestResponseMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context,
        IRequestLogService requestLogService,
        IResponseLogService responseLogService)
    {
        if (ExcludedPathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var traceId = Guid.NewGuid().ToString();
        context.Items["TraceId"] = traceId;
        context.Response.Headers["X-Trace-Id"] = traceId;

        await SaveRequestAsync(context, requestLogService, traceId);

        var stopwatch = Stopwatch.StartNew();
        var originalBody = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            await SaveResponseAsync(context, responseLogService, traceId, stopwatch.ElapsedMilliseconds);
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBody);
            context.Response.Body = originalBody;
        }
    }

    private async Task SaveRequestAsync(HttpContext context, IRequestLogService service, string traceId)
    {
        try
        {
            context.Request.EnableBuffering();
            string body = string.Empty;
            if (context.Request.Body != null)
            {
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }

            var headers = context.Request.Headers
                .ToDictionary(h => h.Key, h => h.Value.ToString());
            var queryParams = context.Request.Query
                .ToDictionary(q => q.Key, q => q.Value.ToString());

            await service.InsertAsync(new RequestProduct
            {
                TraceId = traceId,
                Endpoint = context.Request.Path,
                HttpMethod = context.Request.Method,
                Headers = JsonSerializer.Serialize(headers),
                QueryParams = JsonSerializer.Serialize(queryParams),
                Body = body,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                RequestedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REQUEST_LOG] Gagal menyimpan request | TraceId={TraceId}", traceId);
        }
    }

    private async Task SaveResponseAsync(HttpContext context, IResponseLogService service, string traceId, long elapsedMs)
    {
        try
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            var message = context.Response.StatusCode < 400 ? "Success" : "Failed";

            await service.InsertAsync(new ResponseProduct
            {
                TraceId = traceId,
                StatusCode = context.Response.StatusCode,
                IsSuccess = context.Response.StatusCode < 400,
                Message = message,
                ResponseBody = responseBody,
                ElapsedMs = elapsedMs,
                RespondedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RESPONSE_LOG] Gagal menyimpan response | TraceId={TraceId}", traceId);
        }
    }
}