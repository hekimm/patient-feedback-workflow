using HastaGeriBildirim.Services;
using Microsoft.AspNetCore.Mvc;

namespace HastaGeriBildirim.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly ProductionReadinessValidator _validator;

    public HealthController(ProductionReadinessValidator validator)
    {
        _validator = validator;
    }

    [HttpGet("/health/live")]
    public IActionResult Live()
    {
        return Ok(new { status = "live", utc = DateTime.UtcNow });
    }

    [HttpGet("/health/ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        errors.AddRange(_validator.ValidateConfiguration(includeIntegrationSecrets: false));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        var databaseTask = Task.Run(
            () => _validator.ValidateDatabaseAsync(timeout.Token),
            timeout.Token);
        var completed = await Task.WhenAny(databaseTask, Task.Delay(TimeSpan.FromSeconds(6), cancellationToken));
        if (completed == databaseTask)
            errors.AddRange(await databaseTask);
        else
            errors.Add("Oracle readiness kontrolu zaman asimina ugradi.");

        if (errors.Count > 0)
            return StatusCode(503, new { status = "not_ready", errors });

        return Ok(new { status = "ready", utc = DateTime.UtcNow });
    }
}
