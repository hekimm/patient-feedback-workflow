namespace HastaGeriBildirim.Services;

public class HgbBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HgbBackgroundService> _logger;
    private readonly int _intervalSeconds;

    public HgbBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<HgbBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalSeconds = configuration.GetValue("BackgroundJobs:IntervalSeconds", 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                try
                {
                    var dispatchService = scope.ServiceProvider.GetRequiredService<SurveyDispatchService>();
                    await dispatchService.RunAllAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Anket tetikleme döngüsünde hata");
                }

                try
                {
                    var maintenanceService = scope.ServiceProvider.GetRequiredService<MaintenanceService>();
                    await maintenanceService.RunAllAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bakım döngüsünde hata");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }
}
