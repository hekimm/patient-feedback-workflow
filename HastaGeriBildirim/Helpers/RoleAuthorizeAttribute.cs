using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HastaGeriBildirim.Helpers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RoleAuthorizeAttribute : ActionFilterAttribute
{
    private readonly string[] _allowedRoles;

    public RoleAuthorizeAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var roleCode = context.HttpContext.GetRoleCode();

        if (roleCode != "SYS_ADMIN" && !_allowedRoles.Contains(roleCode))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            return;
        }

        base.OnActionExecuting(context);
    }
}
