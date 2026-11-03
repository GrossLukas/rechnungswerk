using Microsoft.EntityFrameworkCore;
using RechnungsWerk.Web.Data;

namespace RechnungsWerk.Web.Services;

public sealed class CompanyService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    public async Task<CompanyProfile?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == CompanyProfile.SingletonId, cancellationToken);
    }

    public async Task SaveAsync(CompanyForm form, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.CompanyProfiles.FirstOrDefaultAsync(p => p.Id == CompanyProfile.SingletonId, cancellationToken);
        if (profile is null)
        {
            profile = new CompanyProfile();
            db.CompanyProfiles.Add(profile);
        }

        form.ApplyTo(profile);
        profile.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CustomerService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    public async Task<List<Customer>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var customers = await db.Customers.AsNoTracking().ToListAsync(cancellationToken);
        return customers.OrderBy(c => c.Name, GermanOrder.Comparer).ToList();
    }

    public async Task<Customer?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<ServiceResult> SaveAsync(int? id, CustomerForm form, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        Customer? customer;
        if (id is null)
        {
            customer = new Customer { CreatedAt = time.GetUtcNow().UtcDateTime };
            db.Customers.Add(customer);
        }
        else
        {
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (customer is null)
            {
                return ServiceResult.Fail("Diesen Kunden gibt es nicht mehr.");
            }
        }

        form.ApplyTo(customer);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok(customer.Id);
    }

    /// <summary>Bestehende Rechnungen behalten ihre eingefrorenen Kundendaten.</summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is not null)
        {
            db.Customers.Remove(customer);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class ArticleService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    public async Task<List<Article>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var articles = await db.Articles.AsNoTracking().ToListAsync(cancellationToken);
        return articles.OrderBy(a => a.Name, GermanOrder.Comparer).ToList();
    }

    public async Task<Article?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<ServiceResult> SaveAsync(int? id, ArticleForm form, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        Article? article;
        if (id is null)
        {
            article = new Article { CreatedAt = time.GetUtcNow().UtcDateTime };
            db.Articles.Add(article);
        }
        else
        {
            article = await db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (article is null)
            {
                return ServiceResult.Fail("Diesen Artikel gibt es nicht mehr.");
            }
        }

        form.ApplyTo(article);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok(article.Id);
    }

    /// <summary>Rechnungspositionen sind Kopien – gelöschte Artikel ändern keine Rechnung.</summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is not null)
        {
            db.Articles.Remove(article);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>Sortiert Namen wie ein deutsches Telefonbuch (Umlaute an der richtigen Stelle, ohne Groß-/Kleinschreibung).</summary>
internal static class GermanOrder
{
    public static readonly StringComparer Comparer = StringComparer.Create(Core.Formatting.GermanFormat.Culture, ignoreCase: true);
}
