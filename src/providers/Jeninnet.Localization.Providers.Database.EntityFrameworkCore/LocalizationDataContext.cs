//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.EntityFrameworkCore;

/// <summary>
/// Defines the data context for localization operations.
/// </summary>
public interface ILocalizationDataContext {
    /// <summary>
    /// Gets or sets the DbSet for localized strings.
    /// </summary>
    DbSet<LocalizedStringEntry> LocalizedStrings { get; set; }

    /// <summary>
    /// Saves all changes made to the context.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// EF Core DbContext for storing localization entries.
/// </summary>
/// <remarks>
/// Besides the live catalog (<see cref="LocalizedStrings"/>), the context maps the
///  accountability tables: the redacted, hash-chained audit trail
/// (<see cref="AuditRecords"/>), the replay journal for as-of-version reads
/// (<see cref="StringVersions"/>), and the single-row catalog version counter
/// (<see cref="CatalogState"/>).
/// </remarks>
/// <param name="options">The options for the context.</param>
public class LocalizationDataContext(DbContextOptions<LocalizationDataContext> options) : DbContext(options), ILocalizationDataContext {
    /// <summary>
    /// Table of localized strings.
    /// </summary>
    public DbSet<LocalizedStringEntry> LocalizedStrings { get; set; } = default!;

    /// <summary>
    /// Append-only audit trail for governed mutations (redacted: hashes only).
    /// </summary>
    public DbSet<LocalizationAuditRecord> AuditRecords { get; set; } = default!;

    /// <summary>
    /// Append-only version journal backing as-of-version reads.
    /// </summary>
    public DbSet<LocalizedStringVersion> StringVersions { get; set; } = default!;

    /// <summary>
    /// Single-row catalog version counter.
    /// </summary>
    public DbSet<LocalizationCatalogState> CatalogState { get; set; } = default!;

    /// <summary>
    /// Key-level translator notes: one note per resource set and message key.
    /// </summary>
    public DbSet<LocalizedStringNote> StringNotes { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LocalizationCatalogState>()
            .Property(static state => state.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<LocalizationAuditRecord>()
            .HasKey(static record => record.Sequence);

        modelBuilder.Entity<LocalizationAuditRecord>()
            .HasIndex(static record => record.Version);

        modelBuilder.Entity<LocalizedStringVersion>()
            .HasKey(static version => version.Sequence);

        modelBuilder.Entity<LocalizedStringVersion>()
            .HasIndex(static version => new { version.ResourceSet, version.Culture, version.Version });

        modelBuilder.Entity<LocalizedStringNote>()
            .HasIndex(static note => new { note.ResourceSet, note.Name })
            .IsUnique();
    }
}
