using Microsoft.AspNetCore.Mvc;

namespace HastaGeriBildirim.Controllers;

public class KioskController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Start(string token, string? lang)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ViewBag.ErrorMessage = "Token giriniz.";
            return View("Index");
        }

        return RedirectToAction("Start", "Survey", new
        {
            token = token.Trim(),
            lang,
            kiosk = true
        });
    }
}

