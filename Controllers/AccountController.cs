using System.Security.Claims;
using EmpactRecipeOnline.Models;
using EmpactRecipeOnline.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

public sealed class AccountController(JsonDataStore store) : Controller
{
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);
        var login = model.Username.Trim();
        var user = store.Data.Users.FirstOrDefault(x =>
            x.Username.Equals(login, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(x.Email) && x.Email.Equals(login, StringComparison.OrdinalIgnoreCase)));
        if (user is null || !user.IsActive || !user.IsApproved || !PasswordService.Verify(model.Password, user.PasswordHash, user.Salt))
        {
            ModelState.AddModelError(string.Empty, "Invalid unit code / username or password.");
            return View(model);
        }
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email ?? string.Empty), new(ClaimTypes.Role, user.Role.ToString()),
            new("unit", user.Unit), new("unitCode", user.UnitCode ?? string.Empty), new("region", user.Region), new("username", user.Username),
            new("mustChangePassword", user.MustChangePassword.ToString())
        };
        await HttpContext.SignInAsync("EmpactCookie", new ClaimsPrincipal(new ClaimsIdentity(claims, "EmpactCookie")), new AuthenticationProperties { IsPersistent=model.RememberMe });
        store.LogActivity(new ActivityLog { UserId=user.Id, UserName=user.DisplayName, UserRole=user.Role.ToString(), Unit=user.Unit, Region=user.Region, Action="Login", EntityType="User", EntityId=user.Id, EntityName=user.DisplayName });
        if (user.MustChangePassword) return RedirectToAction(nameof(ChangePassword));
        return LocalRedirect(returnUrl ?? "/");
    }

    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = Guid.TryParse(idText, out var id) ? store.Data.Users.FirstOrDefault(x => x.Id == id) : null;
        if (user is null || !PasswordService.Verify(model.CurrentPassword, user.PasswordHash, user.Salt))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "The current password is incorrect.");
            return View(model);
        }
        var password = PasswordService.CreateHash(model.NewPassword);
        user.PasswordHash = password.Hash;
        user.Salt = password.Salt;
        user.MustChangePassword = false;
        store.Save();
        await HttpContext.SignOutAsync("EmpactCookie");
        TempData["Success"] = "Password changed. Please sign in with your new password.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult Register() => RedirectToAction(nameof(Login));

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync("EmpactCookie"); return RedirectToAction(nameof(Login)); }
    public IActionResult AccessDenied() => View();
}
