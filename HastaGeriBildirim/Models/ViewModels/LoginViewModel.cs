using System.ComponentModel.DataAnnotations;

namespace HastaGeriBildirim.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Kullanıcı adı zorunludur")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
