using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RechnungsWerk.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.General);

    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<SmtpSettings> SmtpSettings => Set<SmtpSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CompanyProfile>(entity =>
        {
            entity.Property(p => p.Id).ValueGeneratedNever();
            entity.Property(p => p.DefaultFormat).HasConversion<string>().HasMaxLength(32);
        });

        builder.Entity<Customer>(entity => entity.HasIndex(c => c.Name));

        builder.Entity<Article>(entity => entity.HasIndex(a => a.Name));

        builder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(i => i.Number).IsUnique();
            entity.HasIndex(i => i.IssueDate);
            entity.HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(i => i.PreferredFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(i => i.SentFormat).HasConversion<string>().HasMaxLength(32);
            entity.Ignore(i => i.IsLocked);
            entity.Ignore(i => i.LacksExemptionReason);
            MapSnapshot(entity.Property(i => i.Seller), "SellerJson");
            MapSnapshot(entity.Property(i => i.Buyer), "BuyerJson");
        });

        builder.Entity<SmtpSettings>(entity =>
        {
            entity.Property(s => s.Id).ValueGeneratedNever();
            entity.Property(s => s.Security).HasConversion<string>().HasMaxLength(32);
        });
    }

    /// <summary>Speichert Verkäufer- und Käuferangaben als JSON-Schnappschuss (unveränderliche Records).</summary>
    private static void MapSnapshot<T>(PropertyBuilder<T> property, string column)
        where T : class
    {
        property
            .HasColumnName(column)
            .HasConversion(
                value => JsonSerializer.Serialize(value, SnapshotJson),
                json => JsonSerializer.Deserialize<T>(json, SnapshotJson)!,
                new ValueComparer<T>((a, b) => Equals(a, b), value => value.GetHashCode(), value => value));
    }
}

/// <summary>Für „dotnet ef migrations“: erzeugt den Kontext ohne die ganze Anwendung zu starten.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
