using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using cobaproject.Configuration;
using cobaproject.Helpers;
using cobaproject.Services;
using cobaproject.Services.Interfaces;
using DbUp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
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

const string logTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        // Buat folder log secara eksplisit agar strukturnya langsung terlihat,
        // lalu pakai path absolut berbasis direktori kerja saat startup.
        static string LogsPath(string sub)
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "logs", sub);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "log-.log");
        }

        foreach (var sub in new[] { "aplikasi", "web", "api", "audit" })
        {
            _ = LogsPath(sub);
        }

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        // Terminal hanya menampilkan log dotnet/host (Microsoft.*, System.*, atau fatal tanpa
        // sumber) pada level Warning+ — isi file log aplikasi/web/api/audit tidak ikut tercetak.
        .WriteTo.Logger(l => l
            .Filter.ByIncludingOnly(IsConsoleWorthy)
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning))
        // Akar logs/ dengan grup asal: aplikasi (service/helper/lainnya), web (halaman),
        // api (controller), audit (jejak audit DB dicerminkan ke file).
        .WriteTo.Logger(l => l
            .Filter.ByIncludingOnly(IsOtherSource)
            .WriteTo.File(LogsPath("aplikasi"), rollingInterval: RollingInterval.Day, outputTemplate: logTemplate))
        .WriteTo.Logger(l => l
            .Filter.ByIncludingOnly(Serilog.Filters.Matching.FromSource("cobaproject.Controllers"))
            .WriteTo.File(LogsPath("api"), rollingInterval: RollingInterval.Day, outputTemplate: logTemplate))
        .WriteTo.Logger(l => l
            .Filter.ByIncludingOnly(Serilog.Filters.Matching.FromSource("cobaproject.Pages"))
            .WriteTo.File(LogsPath("web"), rollingInterval: RollingInterval.Day, outputTemplate: logTemplate))
        .WriteTo.Logger(l => l
            .Filter.ByIncludingOnly(IsAuditSource)
            .WriteTo.File(LogsPath("audit"), rollingInterval: RollingInterval.Day, outputTemplate: logTemplate));

    // Catatan: CreateLogger() dipanggil otomatis oleh UseSerilog (sekali) — jangan dipanggil manual.
    // Baris "siap" ditulis setelah app di-build supaya Log.Logger sudah terpasang.
});

static Func<LogEvent, bool> IsSourceOf(string prefix) => evt =>
    evt.Properties.TryGetValue("SourceContext", out var sc)
    && sc.ToString().Trim('"').StartsWith(prefix, StringComparison.Ordinal);

static bool IsAuditSource(LogEvent evt) =>
    evt.Properties.TryGetValue("SourceContext", out var sc)
    && string.Equals(sc.ToString().Trim('"'), "Audit", StringComparison.Ordinal);

static bool IsOtherSource(LogEvent evt) =>
    !IsSourceOf("cobaproject.Controllers")(evt)
    && !IsSourceOf("cobaproject.Pages")(evt)
    && !IsAuditSource(evt);

static bool IsConsoleWorthy(LogEvent evt)
{
    if (!evt.Properties.TryGetValue("SourceContext", out var sc))
    {
        return true;
    }
    var source = sc.ToString().Trim('"');
    return source.StartsWith("Microsoft", StringComparison.Ordinal)
        || source.StartsWith("System", StringComparison.Ordinal);
}

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
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISettingService, SettingService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICourierService, CourierService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDiscountApprovalService, DiscountApprovalService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Panel/Masuk";
        options.AccessDeniedPath = "/Panel/Ditolak";
        options.Cookie.Name = "GKLaku.Auth";
        options.SlidingExpiration = true;
    })
    .AddCookie(CustomerAuth.CustomerScheme, options =>
    {
        options.LoginPath = "/Masuk";
        options.AccessDeniedPath = "/Ditolak";
        options.Cookie.Name = "GKLaku.Customer";
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiJsonForbidHandler>();

builder.Services.AddHttpClient<IFakeStoreService, FakeStoreService>(client =>
    client.BaseAddress = new Uri(builder.Configuration["FakeStoreApi:BaseUrl"]!));

var app = builder.Build();

// Log.Logger sudah terpasang oleh UseSerilog — tulis baris "siap" per kategori agar
// file log langsung lahir & struktur folders terlihat sejak awal.
Log.Information("Log aplikasi siap — service/helper/lainnya tercatat di sini");
Log.ForContext("SourceContext", "cobaproject.Controllers").Information("Log api siap — controller/endpoint tercatat di sini");
Log.ForContext("SourceContext", "cobaproject.Pages").Information("Log web siap — halaman Razor tercatat di sini");
Log.ForContext("SourceContext", "Audit").Information("Log audit siap — jejak audit DB dicerminkan ke sini");

try
{

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
// UseAuthentication hanya memvalidasi scheme default (cookie staf). Cookie
// pelanggan (GKLaku.Customer) perlu di-autentikasi eksplisit: jika principal
// saat ini BUKAN pelanggan (termasuk saat cookie staf ikut terbawa), coba
// cookie pelanggan — dan utamakan di area publik; /Panel dan /api tetap milik
// principal staf/API key.
app.Use(async (context, next) =>
{
    var currentIsCustomer = context.User.Identity?.IsAuthenticated == true
        && string.Equals(context.User.Identity?.AuthenticationType, CustomerAuth.CustomerScheme, StringComparison.OrdinalIgnoreCase);

    if (!currentIsCustomer)
    {
        var result = await context.AuthenticateAsync(CustomerAuth.CustomerScheme);
        if (result.Succeeded && result.Principal is not null)
        {
            var path = context.Request.Path;
            var isStaffArea = path.StartsWithSegments("/Panel", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
            if (!isStaffArea)
            {
                context.User = result.Principal;
            }
        }
    }
    await next();
});
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

}
catch (Exception ex)
{
    // Terminal sengaja senyap — simpan jejak crash ke file agar terbaca tanpa console.
    var crashDir = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "logs"));
    File.WriteAllText(
        Path.Combine(crashDir.FullName, "startup-crash.txt"),
        $"{DateTime.Now:O}{Environment.NewLine}{ex}");
    Log.Fatal(ex, "Aplikasi crash");
    Log.CloseAndFlush();
    return 1;
}

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