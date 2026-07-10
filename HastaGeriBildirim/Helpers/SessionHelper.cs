namespace HastaGeriBildirim.Helpers;

public static class SessionHelper
{
    public static int? GetUserId(this HttpContext httpContext)
    {
        return httpContext.Session.GetInt32("UserId");
    }

    public static string? GetUsername(this HttpContext httpContext)
    {
        return httpContext.Session.GetString("Username");
    }

    public static string? GetFullName(this HttpContext httpContext)
    {
        return httpContext.Session.GetString("FullName");
    }

    public static string? GetRoleCode(this HttpContext httpContext)
    {
        return httpContext.Session.GetString("RoleCode");
    }

    public static bool IsAuthenticated(this HttpContext httpContext)
    {
        return httpContext.Session.GetInt32("UserId").HasValue;
    }
}
