using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using HastaGeriBildirim.Helpers;

namespace HastaGeriBildirim.Controllers;

public class BaseController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!HttpContext.IsAuthenticated())
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return;
        }

        ViewBag.Username = HttpContext.GetUsername();
        ViewBag.FullName = HttpContext.GetFullName();
        ViewBag.RoleCode = HttpContext.GetRoleCode();
        
        base.OnActionExecuting(context);
    }
}
