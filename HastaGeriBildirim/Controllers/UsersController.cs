using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("SYS_ADMIN")]
public class UsersController : BaseController
{
    private readonly UserRepository _userRepository;
    private readonly AuditService _auditService;

    public UsersController(
        UserRepository userRepository,
        AuditService auditService)
    {
        _userRepository = userRepository;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userRepository.GetAllUsersAsync();
        ViewBag.Roles = await _userRepository.GetRolesAsync();
        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string username,
        string fullName,
        string? email,
        string password,
        int roleId)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var userId = await _userRepository.CreateUserAsync(username, fullName, email, passwordHash, roleId);

        await _auditService.AddLogAsync(
            "USER",
            userId,
            "CREATED",
            HttpContext.GetUserId(),
            null,
            $"Kullanıcı oluşturuldu: {username}",
            null);

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id, bool enable)
    {
        await _userRepository.SetUserStatusAsync(id, enable);
        await _auditService.AddLogAsync(
            "USER",
            id,
            enable ? "ENABLED" : "DISABLED",
            HttpContext.GetUserId(),
            null,
            "Kullanıcı durumu değiştirildi",
            null);

        return RedirectToAction("Index");
    }
}
