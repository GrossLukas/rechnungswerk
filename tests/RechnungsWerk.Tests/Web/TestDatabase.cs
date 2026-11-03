using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RechnungsWerk.Web.Data;

namespace RechnungsWerk.Tests.Web;

/// <summary>SQLite im Arbeitsspeicher mit dem echten Migrationsstand – eine Datenbank je Test.</summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public TestDatabase()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var db = Create();
        db.Database.Migrate();
    }

    public DbContextOptions<AppDbContext> Options { get; }

    public IDbContextFactory<AppDbContext> Factory => new ContextFactory(Options);

    public AppDbContext Create() => new(Options);

    public void Dispose() => connection.Dispose();

    private sealed class ContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
