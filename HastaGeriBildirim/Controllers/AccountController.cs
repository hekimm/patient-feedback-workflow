using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Models.ViewModels;
using HastaGeriBildirim.Services;

namespace HastaGeriBildirim.Controllers;

public class AccountController : Controller
{
    private readonly AuthService _authService;

    public AccountController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _authService.ValidateUserAsync(model.Username, model.Password);

        if (user == null)
        {
            ModelState.AddModelError("", "Kullanıcı adı veya şifre hatalı");
            return View(model);
        }

        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("Username", user.Username);
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("RoleCode", user.RoleCode);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        Response.Cookies.Delete(".AspNetCore.Session");
        return RedirectToAction("Login");
    }
}
