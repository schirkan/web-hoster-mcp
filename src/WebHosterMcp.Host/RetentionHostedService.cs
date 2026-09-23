using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebHosterMcp.Core;

namespace WebHosterMcp.Host;

public sealed class RetentionHostedService : BackgroundService
{
    private readonly SiteManager _siteManager;
    private readonly RetentionOptions _options;
    private readonly ILogger<RetentionHostedService> _logger;

    public RetentionHostedService(
        SiteManager siteManager,
        IOptions<RetentionOptions> options,
        ILogger<RetentionHostedService> logger)
    {
        _siteManager = siteManager;
        _options = options.Value;
        _logger = logger;

        if (_options.CheckIntervalSeconds < 1 || _options.CheckIntervalSeconds > 86_400)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.CheckIntervalSeconds),
                _options.CheckIntervalSeconds,
                "Retention:CheckIntervalSeconds muss im Range 1..86400 liegen.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Retention-Service deaktiviert.");
            return;
        }

        await SweepAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.CheckIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepAsync(stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        var sites = await _siteManager.ListAsync(ct);
        var now = DateTime.Now;

        foreach (var site in sites)
        {
            if (site.RetentionSeconds <= 0)
            {
                continue;
            }

            var expiresAt = site.UpdatedAt.AddSeconds(site.RetentionSeconds);
            if (now <= expiresAt)
            {
                continue;
            }

            var ageSeconds = Math.Max(0, (int)(now - site.UpdatedAt).TotalSeconds);
            var deleted = await _siteManager.DeleteAsync(site.SitePath, ct);
            if (deleted)
            {
                _logger.LogInformation(
                    "Site expired: {SitePath} (ttl={Ttl}s, age={Age}s)",
                    site.SitePath,
                    site.RetentionSeconds,
                    ageSeconds);
            }
        }
    }
}
