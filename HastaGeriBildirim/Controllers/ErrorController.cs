using Microsoft.AspNetCore.Mvc;

namespace HastaGeriBildirim.Controllers;

public class ErrorController : Controller
{
    [Route("/Error")]
    public IActionResult Index()
    {
        ViewBag.CorrelationId = HttpContext.Items["CorrelationId"]?.ToString()
            ?? HttpContext.TraceIdentifier;
        return View();
    }
}
