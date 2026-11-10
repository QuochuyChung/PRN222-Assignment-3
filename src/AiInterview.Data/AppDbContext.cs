using System.Text.Json;
using AiInterview.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AiInterview.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Question> Questions => Set<Question>();

    public DbSet<InterviewSession> InterviewSessions => Set<InterviewSession>();

    public DbSet<InterviewTurn> InterviewTurns => Set<InterviewTurn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Question>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).IsRequired().HasMaxLength(2000);
            e.Property(x => x.ReferenceAnswer).IsRequired();
            e.Property(x => x.KeyPoints).HasJsonListConversion();
            e.Property(x => x.SourceChunkIds).HasJsonListConversion();
            e.Property(x => x.SourceDocumentId).HasMaxLength(64);
            e.Property(x => x.BloomLevel).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<InterviewSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.StudentName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<InterviewTurn>(e =>
        {
            e.HasKey(x => x.TurnId);
            e.Property(x => x.TurnId).HasMaxLength(64);
            e.Property(x => x.QuestionText).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.Sources).HasJsonListConversion();
            e.HasOne(x => x.Session)
                .WithMany(s => s.Turns)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.SessionId);
            e.HasIndex(x => x.EndedAt);
        });
    }
}

internal static class PropertyBuilderExtensions
{
    /// <summary>Lưu <c>List&lt;string&gt;</c> thành một cột JSON.</summary>
    public static PropertyBuilder<List<string>> HasJsonListConversion(this PropertyBuilder<List<string>> builder)
    {
        var converter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var comparer = new ValueComparer<List<string>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            v => v.ToList());

        builder.HasConversion(converter);
        builder.Metadata.SetValueComparer(comparer);
        return builder;
    }
}
