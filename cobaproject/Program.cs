using System.Diagnostics;
using System.Reflection;
using cobaproject.Configuration;
using cobaproject.Helpers;
using cobaproject.Services;
using cobaproject.Services.Interfaces;
using DbUp;
using Microsoft.Data.SqlClient;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
builder.Services.AddControllers();
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
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment()
    && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_OPEN_BROWSER")))
{
    app.Lifetime.ApplicationStarted.Register(() =>
        OpenBrowserInEdge("http://localhost:5251/swagger"));
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