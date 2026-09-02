using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using cobaproject.Configuration;
using cobaproject.Helpers;
using cobaproject.Services;
using cobaproject.Services.Interfaces;
using DbUp;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerUI;

// Kunci kultur ke InvariantCulture agar angka selalu memakai titik desimal
// (mis. 150000.50), konsisten antara render form, validasi browser, dan binding.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

// Dapper: cocokkan kolom SNAKE_CASE (RATING_RATE, IS_ACTIVE, ...) ke properti
// PascalCase (RatingRate, IsActive). Tanpa ini, kolom multi-kata ter-baca null.
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// Semua pesan sistem dalam Bahasa Indonesia: binder, validasi, dan API.
static void SetMessagesIndonesia(MvcOptions options)
{
    var p = options.ModelBindingMessageProvider;
    p.SetMissingBindRequiredValueAccessor(field => $"Nilai untuk kolom {field} wajib disertakan.");
    p.SetMissingKeyOrValueAccessor(() => "Nilai wajib disertakan.");
    p.SetMissingRequestBodyRequiredValueAccessor(() => "Body request wajib disertakan.");
    p.SetValueMustBeANumberAccessor(field => $"Kolom {field} harus berupa angka.");
    p.SetValueIsInvalidAccessor(value => $"Nilai '{value}' tidak valid.");
    p.SetValueMustNotBeNullAccessor(field => $"Kolom {field} wajib diisi.");
    p.SetAttemptedValueIsInvalidAccessor((value, field) => $"Nilai '{value}' tidak valid untuk {field}.");
    p.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Nilai '{value}' tidak valid.");
    p.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Nilai yang diberikan tidak valid.");
    p.SetNonPropertyValueMustBeANumberAccessor(() => "Harus berupa angka.");
    p.SetUnknownValueIsInvalidAccessor(value => "Nilai yang diberikan tidak valid.");
}

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
// SuppressModelStateInvalidFilter: controller menangani ModelState sendiri
// (pola ResponseHelper.ValidationError), sehingga 400 otomatis ProblemDetails
// Inggris untuk JSON rusak tidak muncul — semuanya ApiResponse Indonesia.
// PostConfigure<MvcOptions> memastikan pesan binder Indonesia berlaku untuk
// controller dan Razor Pages sekaligus.
builder.Services.AddControllers(options => SetMessagesIndonesia(options));
builder.Services.AddRazorPages();
builder.Services.PostConfigure<ApiBehaviorOptions>(options =>
    options.SuppressModelStateInvalidFilter = true);
builder.Services.PostConfigure<MvcOptions>(SetMessagesIndonesia);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes.Add("ApiKey", new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
            Name = builder.Configuration["ApiKey:HeaderName"] ?? "X-Api-Key",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Masukkan API Key (contoh: TEST123)"
        });

        document.Security ??= new List<Microsoft.OpenApi.OpenApiSecurityRequirement>();
        document.Security.Add(new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("ApiKey", document)] = new List<string>()
        });

        return Task.CompletedTask;
    });
});

builder.Services.AddHttpContextAccessor();

builder.Services.Configure<ApiKeyConfig>(builder.Configuration.GetSection("ApiKey"));
builder.Services.Configure<DatabaseConfig>(builder.Configuration.GetSection("ConnectionStrings"));

builder.Services.AddScoped<IRequestLogService, RequestLogService>();
builder.Services.AddScoped<IResponseLogService, ResponseLogService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.Cookie.Name = "GKLaku.Auth";
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiJsonForbidHandler>();

builder.Services.AddHttpClient<IFakeStoreService, FakeStoreService>(client =>
    client.BaseAddress = new Uri(builder.Configuration["FakeStoreApi:BaseUrl"]!));

var app = builder.Build();

// DB migration via dbup-sqlserver saat startup
try
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
    EnsureDatabase.For.SqlDatabase(connectionString);

    var upgrader = DeployChanges.To
        .SqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .WithVariablesDisabled()
        .LogToConsole()
        .Build();

    var result = upgrader.PerformUpgrade();
    if (!result.Successful)
    {
        Log.Fatal(result.Error, "DB migration gagal");
        throw result.Error;
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gagal menjalankan DB migration saat startup");
    return 1;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "cobaproject v1"));
}

if (app.Urls.Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<RequestResponseMiddleware>();

app.UseAuthentication();
// ApiKeyMiddleware dijalankan setelah UseAuthentication agar principal API key
// (dengan role claim) menimpa principal cookie untuk semua permintaan /api/*.
app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

if (app.Environment.IsDevelopment()
    && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_OPEN_BROWSER")))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = app.Urls.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? "http://localhost:5251";
        url = url.Replace("://0.0.0.0", "://localhost", StringComparison.OrdinalIgnoreCase)
                 .Replace("://+", "://localhost", StringComparison.OrdinalIgnoreCase);
        OpenBrowserInEdge(url);
    });
}

app.Run();

return 0;

static void OpenBrowserInEdge(string url)
{
    try
    {
        var edgePaths = new[]
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
        };

        var edgePath = edgePaths.FirstOrDefault(File.Exists);
        if (edgePath is not null)
        {
            Process.Start(new ProcessStartInfo(edgePath, url) { UseShellExecute = true });
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Gagal membuka browser");
    }
}