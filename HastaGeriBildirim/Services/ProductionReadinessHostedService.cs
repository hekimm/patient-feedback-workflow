namespace HastaGeriBildirim.Services;

public class ProductionReadinessHostedService : IHostedService
{
    private readonly ProductionReadinessValidator _validator;

    public ProductionReadinessHostedService(ProductionReadinessValidator validator)
    {
        _validator = validator;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _validator.ThrowIfProductionInvalid();
        var databaseErrors = await _validator.ValidateDatabaseAsync(cancellationToken);
        var isProduction = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);

        if (isProduction && databaseErrors.Count > 0)
            throw new InvalidOperationException("Production database readiness hatasi: " + string.Join(" | ", databaseErrors));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
