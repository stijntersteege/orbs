using Orbs.Application.Accounts;
using Orbs.Application.Channels;
using Orbs.Application.Context;
using Orbs.Application.Jobs;
using Orbs.Application.Schedules;

namespace Orbs.Api.Hosting;

/// <summary>
/// Runs background jobs inside the Orbs process: every few seconds it picks up whatever is due.
/// <c>Orbs:Jobs:Enabled=false</c> turns it off (tests run jobs by hand); <c>Orbs:Jobs:PollSeconds</c> sets the pace.
/// </summary>
internal sealed partial class JobWorker(JobRunner runner, ChannelRelay channels, IServiceScopeFactory scopes, IConfiguration configuration, ILogger<JobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Orbs:Jobs:Enabled", true))
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("Orbs:Jobs:PollSeconds", 5)));
        await runner.RecoverAsync(stoppingToken);
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await using (var scope = scopes.CreateAsyncScope())
                {
                    scope.ServiceProvider.GetRequiredService<CurrentUser>().ActAsSystem();
                    await scope.ServiceProvider.GetRequiredService<Housekeeping>().EnsureScheduledAsync(stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<ScheduleDispatcher>().DispatchDueAsync(stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<ContextSyncDispatcher>().DispatchDueAsync(stoppingToken);
                }

                await runner.RunDueAsync(stoppingToken);
                await channels.PollAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailure(logger, ex, interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Running background jobs failed; trying again in {Interval}.")]
    private static partial void LogFailure(ILogger logger, Exception exception, TimeSpan interval);
}
