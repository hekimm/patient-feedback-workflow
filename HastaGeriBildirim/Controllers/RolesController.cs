using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Models.ViewModels;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("SYS_ADMIN")]
public class RolesController : BaseController
{
    private readonly UserRepository _userRepository;

    public RolesController(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IActionResult> Index()
    {
        var model = new RolesViewModel
        {
            Roles = await _userRepository.GetRolesAsync(),
            Permissions = await _userRepository.GetRolePermissionsAsync()
        };

        return View(model);
    }
}
