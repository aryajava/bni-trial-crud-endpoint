using cobaproject.Models;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Helpers;

public static class ResponseHelper
{
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

    public static IResult Success<T>(HttpContext context, T? data, string message = "Success")
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
                new List<string> { "Resource not found." }),
            statusCode: StatusCodes.Status404NotFound);

    public static IResult Conflict(HttpContext context, string message = "Data telah diubah oleh proses lain. Silakan ambil data terbaru.",
        List<string>? errors = null) =>
        Results.Json(
            Build<object>(context, false, StatusCodes.Status409Conflict, message, null,
                errors ?? new List<string> { "Optimistic concurrency conflict." }),
            statusCode: StatusCodes.Status409Conflict);

    public static IResult ValidationError(HttpContext context, List<string> errors,
        string message = "Validasi gagal.") =>
        Results.Json(
            Build<object>(context, false, StatusCodes.Status422UnprocessableEntity, message, null, errors),
            statusCode: StatusCodes.Status422UnprocessableEntity);

    public static IResult BadGateway(HttpContext context, string message = "External API tidak dapat diakses.") =>
        Results.Json(
            Build<object>(context, false, StatusCodes.Status502BadGateway, message, null,
                new List<string> { "External API unavailable." }),
            statusCode: StatusCodes.Status502BadGateway);

    public static IResult Error(HttpContext context, Exception ex,
        string message = "Terjadi kesalahan internal server.") =>
        Results.Json(
            Build<object>(context, false, StatusCodes.Status500InternalServerError, message, null,
                new List<string> { ex.Message }),
            statusCode: StatusCodes.Status500InternalServerError);
}