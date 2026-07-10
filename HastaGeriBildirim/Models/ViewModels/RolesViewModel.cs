using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Models.ViewModels;

public class RolesViewModel
{
    public List<UserRepository.RoleInfo> Roles { get; set; } = new();
    public List<UserRepository.RolePermissionRow> Permissions { get; set; } = new();
}

