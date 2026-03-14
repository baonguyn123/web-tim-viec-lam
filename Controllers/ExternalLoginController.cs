using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using web_jobs.Models;

public class ExternalLoginController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;

    public ExternalLoginController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Google(string returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(GoogleResponse), new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl);
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public async Task<IActionResult> GoogleResponse(string returnUrl = null)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null) return RedirectToAction("Login", "Account");

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);

        if (signInResult.Succeeded)
        {
            return LocalRedirect(returnUrl ?? "/");
        }
        else
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded) return RedirectToAction("Login", "Account");
            }

            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded) return RedirectToAction("Login", "Account");

            await _signInManager.SignInAsync(user, false);
            return LocalRedirect(returnUrl ?? "/");
        }
    }
}
