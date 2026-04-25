using CityDiscovery.RecommendationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

public class RecommendationDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RecommendationDbContext(DbContextOptions<RecommendationDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessedEvent> ProcessedEvents { get; set; }
    public DbSet<InteractionLog> InteractionLogs { get; set; }
    public DbSet<VenueEmbedding> VenueEmbeddings { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<DismissedVenue> DismissedVenues { get; set; }
    public DbSet<UserPreference> UserPreferences { get; set; }
    public DbSet<PostVenueMapping> PostVenueMappings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).ValueGeneratedNever();
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ProcessedAt).IsRequired();
            entity.HasIndex(e => e.EventId).IsUnique(); 
        });

        modelBuilder.Entity<InteractionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.VenueId).IsRequired();
            entity.Property(e => e.SessionId).IsRequired();
            entity.Property(e => e.InteractionType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Weight).IsRequired();
            entity.Property(e => e.TimeDecayWeight).IsRequired();
            entity.Property(e => e.Timestamp).IsRequired();
            
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.VenueId);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => new { e.UserId, e.VenueId });
        });

        modelBuilder.Entity<VenueEmbedding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VenueId).IsRequired();

            // float[] → JSON string for SQL Server
            entity.Property(e => e.Embedding)
                .IsRequired()
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonOptions),
                    v => JsonSerializer.Deserialize<float[]>(v, JsonOptions) ?? Array.Empty<float>())
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.Categories)
                .HasConversion(
                    v => string.Join("\x1f", v ?? new List<string>()),
                    v => (v ?? "").Split('\x1f', StringSplitOptions.RemoveEmptyEntries).ToList())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));
            entity.Property(e => e.Categories).HasMaxLength(2000);

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.VenueId).IsUnique();
            entity.Property(e => e.CityId).HasColumnType("int");
            entity.HasIndex(e => e.CityId);
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).ValueGeneratedNever();

            // float[] → JSON string for SQL Server
            entity.Property(e => e.Embedding)
                .IsRequired()
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonOptions),
                    v => JsonSerializer.Deserialize<float[]>(v, JsonOptions) ?? Array.Empty<float>())
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.LastUpdatedAt).IsRequired();
        });

        modelBuilder.Entity<DismissedVenue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.VenueId).IsRequired();
            entity.Property(e => e.DismissedAt).IsRequired();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.VenueId }).IsUnique();
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).ValueGeneratedNever();

            entity.Property(e => e.PreferredCategories)
                .HasConversion(
                    v => string.Join("\x1f", v ?? new List<string>()),
                    v => (v ?? "").Split('\x1f', StringSplitOptions.RemoveEmptyEntries).ToList())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));
            entity.Property(e => e.PreferredCategories).HasMaxLength(2000);

            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<PostVenueMapping>(entity =>
        {
            entity.HasKey(e => e.PostId);
            entity.Property(e => e.PostId).ValueGeneratedNever();
            entity.Property(e => e.VenueId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.VenueId);
        });
    }
}
