using Aurik.Monitoring.Application.BackgroundProcessing;
using Aurik.Monitoring.Application.DerivedView;
using Aurik.Monitoring.Application.Ingestion;
using Aurik.Monitoring.Application.Normalization;
using Aurik.Monitoring.Application.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aurik.Monitoring.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Normalizers — add a new INormalizer here to onboard a new vendor.
        services.AddSingleton<INormalizer, PulseForgeNormalizer>();
        services.AddSingleton<INormalizer, ThermexWatchNormalizer>();
        services.AddSingleton<INormalizer, MaintaFlowNormalizer>();
        services.AddSingleton<INormalizerFactory, NormalizerFactory>();

        services.AddSingleton<IDerivedStatusCalculator, DerivedStatusCalculator>();

        // Queue is a singleton; worker is the BackgroundService consumer.
        services.AddSingleton<IProcessingQueue>(_ => new ChannelProcessingQueue(capacity: 1024));
        services.AddHostedService<NormalizationWorker>();

        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<IPayloadProcessor, PayloadProcessor>();

        return services;
    }
}
