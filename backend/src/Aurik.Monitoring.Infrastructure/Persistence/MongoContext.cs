using Aurik.Monitoring.Domain.Entities;
using Aurik.Monitoring.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Aurik.Monitoring.Infrastructure.Persistence;

/// <summary>
/// Owns the MongoClient + IMongoDatabase and exposes typed collections.
/// Registered as a singleton — MongoClient is thread-safe and connection-pooled.
/// </summary>
public sealed class MongoContext
{
    public IMongoCollection<RawPayload> RawPayloads { get; }
    public IMongoCollection<NormalizedEvent> NormalizedEvents { get; }
    public IMongoCollection<Machine> Machines { get; }
    public IMongoCollection<Line> Lines { get; }
    public IMongoCollection<MachineOperationalView> MachineViews { get; }
    public IMongoCollection<DeadLetterEntry> DeadLetters { get; }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    private static int _conventionsRegistered;

    public MongoContext(IOptions<MongoOptions> options, ILogger<MongoContext> log)
    {
        EnsureConventionsRegistered();

        Client = new MongoClient(options.Value.ConnectionString);
        Database = Client.GetDatabase(options.Value.DatabaseName);

        RawPayloads = Database.GetCollection<RawPayload>("raw_payloads");
        NormalizedEvents = Database.GetCollection<NormalizedEvent>("normalized_events");
        Machines = Database.GetCollection<Machine>("machines");
        Lines = Database.GetCollection<Line>("lines");
        MachineViews = Database.GetCollection<MachineOperationalView>("machine_views");
        DeadLetters = Database.GetCollection<DeadLetterEntry>("dead_letters");

        log.LogInformation("Mongo connected: db={Db}", options.Value.DatabaseName);
    }

    public async Task EnsureIndexesAsync(CancellationToken ct)
    {
        // Raw payloads: unique idempotency key + lookup by state.
        await RawPayloads.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<RawPayload>(
                Builders<RawPayload>.IndexKeys.Ascending(p => p.IdempotencyKey),
                new CreateIndexOptions { Unique = true, Name = "ux_raw_idempotency" }),
            new CreateIndexModel<RawPayload>(
                Builders<RawPayload>.IndexKeys.Ascending(p => p.State).Descending(p => p.ReceivedAt),
                new CreateIndexOptions { Name = "ix_raw_state_received" })
        }, ct).ConfigureAwait(false);

        // Normalized events: unique per (vendor, vendor_event_id) so re-deliveries collapse safely.
        await NormalizedEvents.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<NormalizedEvent>(
                Builders<NormalizedEvent>.IndexKeys.Ascending(e => e.IdempotencyKey),
                new CreateIndexOptions { Unique = true, Name = "ux_event_idempotency" }),
            new CreateIndexModel<NormalizedEvent>(
                Builders<NormalizedEvent>.IndexKeys.Ascending(e => e.MachineId).Descending(e => e.EventTimeUtc),
                new CreateIndexOptions { Name = "ix_event_machine_time" })
        }, ct).ConfigureAwait(false);

        // Machine views: query by plant/line/status.
        // (machineId is the _id, so it's already uniquely indexed by the server — no need to add ux_view_machine.)
        await MachineViews.Indexes.CreateOneAsync(
            new CreateIndexModel<MachineOperationalView>(
                Builders<MachineOperationalView>.IndexKeys.Ascending(v => v.PlantId).Ascending(v => v.LineId),
                new CreateIndexOptions { Name = "ix_view_plant_line" }),
            cancellationToken: ct).ConfigureAwait(false);

        // Machine.MachineId is the _id — uniqueness is enforced by the system index.

        // Line uses a generated _id; uniqueness lives on the compound (plantId, lineId).
        await Lines.Indexes.CreateOneAsync(
            new CreateIndexModel<Line>(
                Builders<Line>.IndexKeys.Ascending(l => l.PlantId).Ascending(l => l.LineId),
                new CreateIndexOptions { Unique = true, Name = "ux_line_plant_line" }),
            cancellationToken: ct).ConfigureAwait(false);
    }

    private static void EnsureConventionsRegistered()
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 1) return;

        var pack = new ConventionPack
        {
            new IgnoreExtraElementsConvention(true),
            new EnumRepresentationConvention(BsonType.String),
            new CamelCaseElementNameConvention()
        };
        ConventionRegistry.Register("AurikConventions", pack, _ => true);

        // Map domain entities. Using BsonClassMap directly avoids attributes on Domain (Domain stays infra-free).
        if (!BsonClassMap.IsClassMapRegistered(typeof(RawPayload)))
        {
            BsonClassMap.RegisterClassMap<RawPayload>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(p => p.Id);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(NormalizedEvent)))
        {
            BsonClassMap.RegisterClassMap<NormalizedEvent>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(p => p.Id);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(Machine)))
        {
            BsonClassMap.RegisterClassMap<Machine>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(m => m.MachineId);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(Line)))
        {
            // No _id mapping — let the driver assign an ObjectId; natural key is (plantId, lineId).
            BsonClassMap.RegisterClassMap<Line>(cm => cm.AutoMap());
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(MachineOperationalView)))
        {
            BsonClassMap.RegisterClassMap<MachineOperationalView>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(v => v.MachineId);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(DeadLetterEntry)))
        {
            BsonClassMap.RegisterClassMap<DeadLetterEntry>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(d => d.Id);
            });
        }
    }
}
