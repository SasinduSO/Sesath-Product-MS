using Microsoft.EntityFrameworkCore;
using Serilog;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Data;
using WaterlilyInventory.Web.Filters;
using WaterlilyInventory.Web.Middleware;
using WaterlilyInventory.Web.Repositories.Implementations;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Implementations;
using WaterlilyInventory.Web.Services.Interfaces;
using WaterlilyInventory.Web.Services.Parsing;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog structured logging: daily rolling files in application-relative Logs/ folder
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(builder.Environment.ContentRootPath, "Logs", "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        fileSizeLimitBytes: 10 * 1024 * 1024,
        rollOnFileSizeLimit: true,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Configure Antiforgery: header name RequestVerificationToken for AJAX requests
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Add services to the container and register the global AjaxAntiforgeryFilter and user-friendly model binding error messages
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AjaxAntiforgeryFilter>();
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((value, field) =>
        $"{field} cannot contain letters or invalid characters. Please enter a valid number.");
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(field =>
        $"{field} must be a valid number without letters.");
});

// Register ApplicationDbContext with scoped lifetime
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Repositories with scoped lifetime
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// Register Memory Cache and Cache Abstraction (Singleton infrastructure — never captures scoped dependencies)
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAppCache, AppCache>();

// Register Application Services with scoped lifetime
builder.Services.AddScoped<IProductSpreadsheetParser, WaterlilyInventory.Web.Services.Parsing.ProductSpreadsheetParser>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IReportService, ReportService>();

var app = builder.Build();

// Global exception handling middleware registered earliest to catch all unhandled errors
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Serilog HTTP request logging captures request path, status, and duration
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

try
{
    Log.Information("Starting WaterlilyInventory Web Application.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WaterlilyInventory Web Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
