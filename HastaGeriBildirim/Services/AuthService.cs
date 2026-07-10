using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Services;

public class AuthService
{
    private readonly UserRepository _userRepository;

    public AuthService(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User?> ValidateUserAsync(string username, string password)
    {
        var user = await _userRepository.GetUserByUsernameAsync(username);

        if (user == null || !user.IsActive)
            return null;

        if (string.IsNullOrEmpty(user.PasswordHash))
            return null;

        try
        {
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return null;
        }

        return user;
    }
}
