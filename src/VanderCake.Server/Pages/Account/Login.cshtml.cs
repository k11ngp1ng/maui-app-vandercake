using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VanderCake.Domain.Customers;
using VanderCake.Infrastructure.Identity;

namespace VanderCake.Server.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [BindProperty, Required, DataType(DataType.Password), StringLength(200)]
    public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }
    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await users.FindByEmailAsync(Email);
        if (user is not null && user.IsActive)
        {
            var result = await signIn.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
            if (result.Succeeded && await users.IsInRoleAsync(user, RoleNames.Administrator))
            {
                await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme,
                    await signIn.CreateUserPrincipalAsync(user), new AuthenticationProperties { IsPersistent = false });
                return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/Admin");
            }
        }
        ModelState.AddModelError("", "Invalid credentials or administrator access unavailable.");
        return Page();
    }
}
