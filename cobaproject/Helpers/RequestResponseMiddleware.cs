using System.Diagnostics;
using System.Text;
using System.Text.Json;
using cobaproject.Models;
using cobaproject.Services.Interfaces;

namespace cobaproject.Helpers;

public class RequestResponseMiddleware
{
    private static readonly string[] LoggedPathPrefixes = ["/api", "/Panel"];

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
        // Audit trail internal toko: hanya permintaan API dan area pengurus yang
        // dicatat; lalu lintas publik (katalog, keranjang) tidak membanjiri tabel.
        if (!LoggedPathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
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
                .ToDictionary(h => h.Key, h => h.Value.ToString())
                .ToDictionary(kv => kv.Key, RedactHeader);
            var queryParams = context.Request.Query
                .ToDictionary(q => q.Key, q => q.Value.ToString());

            await service.InsertAsync(new RequestProduct
            {
                TraceId = traceId,
                Endpoint = context.Request.Path,
                HttpMethod = context.Request.Method,
                Headers = JsonSerializer.Serialize(headers),
                QueryParams = JsonSerializer.Serialize(queryParams),
                Body = RedactBody(body),
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

            var message = context.Response.StatusCode < 400 ? "Berhasil" : "Gagal";

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

    private static KeyValuePair<string, string> RedactHeader(KeyValuePair<string, string> header) =>
        header.Key.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase)
            ? new KeyValuePair<string, string>(header.Key, "***")
            : header;

    private static string RedactBody(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = RedactNode(document.RootElement);
            return JsonSerializer.Serialize(root);
        }
        catch (JsonException)
        {
            return RedactRaw(body);
        }
    }

    private static object? RedactNode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return element.EnumerateObject().ToDictionary(
                    p => p.Name,
                    p => IsSensitiveKey(p.Name) ? "***" : RedactNode(p.Value));
            case JsonValueKind.Array:
                return element.EnumerateArray().Select(RedactNode).ToArray();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return element.TryGetDecimal(out var d) ? d : element.GetRawText();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }

    private static bool IsSensitiveKey(string name) =>
        name.Contains("password", StringComparison.OrdinalIgnoreCase)
        || name.Contains("secretkey", StringComparison.OrdinalIgnoreCase)
        || name.Contains("apikey", StringComparison.OrdinalIgnoreCase);

    private static string RedactRaw(string body) =>
        System.Text.RegularExpressions.Regex.Replace(body,
            "(?i)(\"(?:password|secretkey|apikey)\"\\s*:\\s*\")[^\"]*(\")", "$1***$2");
}