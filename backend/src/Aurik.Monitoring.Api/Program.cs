using Aurik.Monitoring.Api.Configuration;
using Aurik.Monitoring.Api.Middleware;
using Aurik.Monitoring.Application.DependencyInjection;
using Aurik.Monitoring.Infrastructure.DependencyInjection;
using Aurik.Monitoring.Infrastructure.Configuration;
using Aurik.Monitoring.Infrastructure.Persistence;
using Aurik.Monitoring.Infrastructure.Seeding;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // snake_case JSON to match the brief's conceptual schema (machine_id, plant_id, etc.).
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    o.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Aurik Monitoring API",
        Version = "v1",
        Description = "Industrial equipment monitoring backend — vendor ingestion, normalization, derived machine attention view."
    });
    o.AddSecurityDefinition("VendorApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Per-vendor API key (required for /api/ingestion/*)."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "VendorApiKey" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Bring up indexes and reference data once on startup.
using (var scope = app.Services.CreateScope())
{
    var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();
    await mongo.EnsureIndexesAsync(CancellationToken.None);

    var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
    if (seedOptions.RunOnStartup)
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>();
        await seeder.SeedAsync(CancellationToken.None);
    }
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();
app.UseMiddleware<ApiKeyAuthMiddleware>();

// Swashbuckle still generates the OpenAPI JSON at /swagger/v1/swagger.json.
// We render it with Scalar (modern UI) instead of Swagger UI.
app.UseSwagger();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Aurik Monitoring API")
           .WithTheme(ScalarTheme.Purple)
           .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl)
           .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json");
});

// Friendly redirect: root → Scalar UI.
app.MapGet("/", () => Results.Redirect("/scalar/v1"));

app.MapControllers();

app.Run();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program { }
