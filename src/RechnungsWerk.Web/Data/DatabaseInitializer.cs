using Microsoft.EntityFrameworkCore;
using RechnungsWerk.Web.Infrastructure;

namespace RechnungsWerk.Web.Data;

public static class DatabaseInitializer
{
    /// <summary>Bringt die Datenbank auf den neuesten Stand und legt bei offener Ersteinrichtung den Einrichtungscode an.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        // WAL erlaubt Lesen, während geschrieben wird – angenehmer bei mehreren offenen Tabs.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

        if (!await db.Users.AnyAsync(cancellationToken))
        {
            var setupCode = scope.ServiceProvider.GetRequiredService<SetupCodeStore>();
            setupCode.EnsureCreated();
            setupCode.AnnounceLocation();
        }
    }
}
