using Libris.Application.Abstractions.Covers;
using Libris.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Libris.Infrastructure.Covers;

/// <summary>Deletes covers uploaded for a form that was never saved.</summary>
public sealed partial class CoverJanitor : BackgroundService
{
    private static readonly TimeSpan Grace = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ICoverStorage _storage;
    private readonly IOptions<CoverOptions> _options;
    private readonly ILogger<CoverJanitor> _logger;

    public CoverJanitor(IServiceScopeFactory scopes, ICoverStorage storage, IOptions<CoverOptions> options, ILogger<CoverJanitor> logger)
    {
        _scopes = scopes;
        _storage = storage;
        _options = options;
        _logger = logger;
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

    /// <summary>
    /// A failed sweep is logged and retried next time: an exception escaping a BackgroundService stops the
    /// whole host by default, and the shared Postgres is briefly down whenever BillFolder recreates it.
    /// </summary>
    public async Task<bool> TrySweepAsync(DateTime now, CancellationToken ct)
    {
        try
        {
            await SweepAsync(now, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogSweepFailed(_logger, ex);
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(options.JanitorStartupDelaySeconds), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.JanitorIntervalSeconds));
            do
            {
                await TrySweepAsync(DateTime.UtcNow, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cover sweep failed; retrying on the next run.")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
