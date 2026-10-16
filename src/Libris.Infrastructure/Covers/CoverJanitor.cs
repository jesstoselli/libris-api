using Libris.Application.Abstractions.Covers;
using Libris.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Libris.Infrastructure.Covers;

/// <summary>Deletes covers uploaded for a form that was never saved.</summary>
public sealed class CoverJanitor : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan Grace = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ICoverStorage _storage;

    public CoverJanitor(IServiceScopeFactory scopes, ICoverStorage storage)
    {
        _scopes = scopes;
        _storage = storage;
    }

    public async Task SweepAsync(DateTime now, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cutoff = now - Grace;

        var orphans = await db.Covers
            .Where(c => c.CreatedAt < cutoff && !db.Books.Any(b => b.CoverId == c.Id))
            .ToListAsync(ct);
        if (orphans.Count == 0)
        {
            return;
        }

        db.Covers.RemoveRange(orphans);
        await db.SaveChangesAsync(ct);
        foreach (var cover in orphans)
        {
            _storage.Delete(cover.Id);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepAsync(DateTime.UtcNow, stoppingToken);
        }
    }
}
