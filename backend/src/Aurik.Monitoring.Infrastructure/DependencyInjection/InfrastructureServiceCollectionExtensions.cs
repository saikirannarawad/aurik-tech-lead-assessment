using Aurik.Monitoring.Application.Abstractions.Persistence;
using Aurik.Monitoring.Infrastructure.Configuration;
using Aurik.Monitoring.Infrastructure.Persistence;
using Aurik.Monitoring.Infrastructure.Persistence.Repositories;
using Aurik.Monitoring.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aurik.Monitoring.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<MongoOptions>(config.GetSection(MongoOptions.SectionName));
        services.Configure<SeedOptions>(config.GetSection(SeedOptions.SectionName));

        services.AddSingleton<MongoContext>();

        services.AddScoped<IRawPayloadRepository, RawPayloadRepository>();
        services.AddScoped<INormalizedEventRepository, NormalizedEventRepository>();
        services.AddScoped<IMachineRepository, MachineRepository>();
        services.AddScoped<ILineRepository, LineRepository>();
        services.AddScoped<IMachineViewRepository, MachineViewRepository>();
        services.AddScoped<IDeadLetterRepository, DeadLetterRepository>();

        services.AddScoped<IReferenceDataSeeder, ReferenceDataSeeder>();

        return services;
    }
}
