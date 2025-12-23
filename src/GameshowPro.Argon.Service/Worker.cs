namespace Argon.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Tools.CheckAndRachetTimeResult timeResult = Tools.CheckAndRachetTime();
            if (timeResult.DateTimeOffset.HasValue) 
            {
                if (timeResult.Delta >= TimeSpan.Zero)
                {
                    _logger.LogInformation("Clock set to {time}, a correction of {delta}", timeResult.DateTimeOffset, timeResult.Delta);
                }
                else
                {
                    _logger.LogInformation("Clock was running ahead by {delta}, so no set was possible", -timeResult.Delta);
                }
            }
            else
            {
                _logger.LogError("Clock correction of {delta} required but not successful", timeResult.Delta);
            }
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
