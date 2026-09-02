using System.Text.Json;
using cobaproject.Models;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Helpers;

public static class ResponseHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string GetTraceId(HttpContext context) =>
        context.Items["TraceId"]?.ToString() ?? Guid.NewGuid().ToString();

    private static ApiResponse<T> Build<T>(HttpContext context, bool isSuccess, int statusCode,
        string message, T? data, List<string>? errors) =>
        new()
        {
            TraceId = GetTraceId(context),
            IsSuccess = isSuccess,
            StatusCode = statusCode,
            Message = message,
            Data = data,
            Errors = errors,
            Timestamp = DateTimeOffset.Now
        };

    private static int GetSuccessStatusCode(string httpMethod, object? data) => httpMethod switch
    {
        "POST" => StatusCodes.Status201Created,
        "DELETE" => data is null ? StatusCodes.Status204NoContent : StatusCodes.Status200OK,
        _ => StatusCodes.Status200OK
    };

    public static IResult Success<T>(HttpContext context, T? data, string message = "Berhasil")
    {
        var statusCode = GetSuccessStatusCode(context.Request.Method, data);
        if (statusCode == StatusCodes.Status204NoContent)
            return Results.NoContent();

        return Results.Json(
            Build(context, true, statusCode, message, data, null),
            statusCode: statusCode);
    }

    public static IResult NotFound(HttpContext context, string message = "Data tidak ditemukan.") =>
        Results.Json(
            Build<object>(context, false, StatusCodes.Status404NotFound, message, null,
                new List<string> { "Data tidak ditemukan." }),
            statusCode: StatusCodes.Status404NotFound);

    public static IResult Conflict(HttpContext context, string message = "Data telah diubah oleh proses lain. Silakan ambil data terbaru.",
        List<string>? errors = null) =>
        Results.Json(
            Build<object>(context, false, StatusCodes.Status409Conflict, message, null,
                errors ?? new List<string> { "Data telah diubah oleh proses lain." }),
            statusCode: StatusCodes.Status409Conflict);

    public static IResult ValidationError(HttpContext context, List<string> errors,
        string message = "Validasi gagal.")
    {
        // Pesan dari input formatter JSON (mis. "The JSON value could not be
        // converted ...") diterjemahkan agar seluruh pesan tetap Bahasa Indonesia.
        var mapped = errors
            .Select(e => e.Contains("JSON value", StringComparison.OrdinalIgnoreCase)
                || e.Contains("malformed", StringComparison.OrdinalIgnoreCase)
                || e.StartsWith("The request body", StringComparison.OrdinalIgnoreCase)
                ? "Format JSON pada body request tidak valid."
                : e)
            .ToList();

        return Results.Json(
            Build<object>(context, false, StatusCodes.Status422UnprocessableEntity, message, null, mapped),
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    public static IResult BadGateway(HttpContext context, string message = "External API tidak dapat diakses.") =>
        Results.Json(
            Build<object>(context, false, StatusCodes.Status502BadGateway, message, null,
                new List<string> { "API eksternal tidak dapat diakses." }),
            statusCode: StatusCodes.Status502BadGateway);

    public static async Task WriteUnauthorizedAsync(HttpContext context,
        string message = "API Key tidak valid atau tidak disertakan.",
        List<string>? errors = null)
    {
        var response = Build<object>(context, false, StatusCodes.Status401Unauthorized, message, null,
            errors ?? new List<string> { "Kunci API tidak ada atau tidak valid." });
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    public static async Task WriteForbiddenAsync(HttpContext context,
        string message = "Akses ditolak. Anda tidak memiliki hak untuk operasi ini.",
        List<string>? errors = null)
    {
        var response = Build<object>(context, false, StatusCodes.Status403Forbidden, message, null,
            errors ?? new List<string> { "Forbidden: role tidak memenuhi syarat." });
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    public static IResult Forbidden(HttpContext context, string message = "Akses ditolak. Anda tidak memiliki hak untuk operasi ini.") =>
        Results.Json(
            Build<object>(context, false, StatusCodes.Status403Forbidden, message, null,
                new List<string> { "Anda tidak memiliki hak untuk operasi ini." }),
            statusCode: StatusCodes.Status403Forbidden);

    public static IResult Error(HttpContext context, Exception ex,
        string message = "Terjadi kesalahan internal server.") =>
        Results.Json(
            Build<object>(context, false, StatusCodes.Status500InternalServerError, message, null,
                new List<string> { ex.Message }),
            statusCode: StatusCodes.Status500InternalServerError);
}