using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using frontida4baby.Web.Models.Entities;
using frontida4baby.Web.Models.ViewModels;
using frontida4baby.Web.Services;
using Microsoft.Extensions.Logging;

namespace frontida4baby.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailService       _email;
    private readonly IAppLogService      _log;
    private readonly INotificationService _notifications;
    private readonly IUserEmailService   _userEmail;
    private readonly IConfiguration      _config;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser>   userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailService       email,
        IAppLogService      log,
        INotificationService notifications,
        IUserEmailService   userEmail,
        IConfiguration      config,
        ILogger<AccountController> logger)
    {
        _userManager   = userManager;
        _signInManager = signInManager;
        _email         = email;
        _log           = log;
        _notifications = notifications;
        _userEmail     = userEmail;
        _config        = config;
        _logger        = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        ViewData["ExternalProviders"] =
            (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                IsCaregiver = model.IsCaregiver,
                HasAcceptedTerms = true,
                TermsAcceptedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                await _log.LogAsync(AppLogLevel.Info, "Account", $"Registered: {model.Email}", userId: user.Id);

                // Build confirm URL on the request thread (Url.Action needs HttpContext)
                var verifyToken    = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmUrl     = Url.Action(nameof(ConfirmEmail), "Account",
                    new { userId = user.Id, token = verifyToken }, Request.Scheme) ?? "";

                // Send email verification link (fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try { await SendVerificationEmailAsync(user, confirmUrl); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send verification email to {Email}", model.Email);
                    }
                });

                // Fire-and-forget admin notification
                _ = Task.Run(async () =>
                {
                    try { await _notifications.NotifyNewRegistrationAsync(model.Email); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send new registration notification for {Email}", model.Email);
                    }
                });

                return RedirectToAction(nameof(VerifyEmailSent));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["ExternalProviders"] =
            (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["ExternalProviders"] =
            (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var loggedIn = await _userManager.FindByEmailAsync(model.Email);
                await _log.LogAsync(AppLogLevel.Info, "Account", $"Login: {model.Email}", userId: loggedIn?.Id);
                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked out due to too many failed attempts. Please try again later.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        model.Password = string.Empty;
        ModelState.Remove(nameof(model.Password));
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account",
            new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return RedirectToAction(nameof(Login));

        // Try to sign in with existing external login record
        var result = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (result.Succeeded)
            return RedirectToLocal(returnUrl);

        // No linked login — try to find existing user by email
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);

        // Only trust the email if the provider asserts it is verified
        var emailVerifiedClaim = info.Principal.FindFirstValue("email_verified");
        bool providerVerifiedEmail = string.Equals(emailVerifiedClaim, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(emailVerifiedClaim, "True", StringComparison.Ordinal);

        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser is not null)
            {
                if (!providerVerifiedEmail)
                {
                    // Provider did not verify this email — do not auto-link (account takeover risk)
                    ModelState.AddModelError(string.Empty,
                        "An account with this email already exists. Please log in with your password to link this provider.");
                    ViewData["ReturnUrl"] = returnUrl;
                    ViewData["ExternalProviders"] =
                        (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
                    return View(nameof(Login), new Models.ViewModels.LoginViewModel { Email = email });
                }

                // Link the external login to the existing account and sign in
                await _userManager.AddLoginAsync(existingUser, info);
                await _signInManager.SignInAsync(existingUser, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            // Auto-create a new account
            var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "";
            var lastName  = info.Principal.FindFirstValue(ClaimTypes.Surname)   ?? "";

            var newUser = new ApplicationUser
            {
                UserName       = email,
                Email          = email,
                EmailConfirmed = true,
                FirstName      = firstName,
                LastName       = lastName
            };

            var createResult = await _userManager.CreateAsync(newUser);
            if (createResult.Succeeded)
            {
                await _userManager.AddLoginAsync(newUser, info);
                await _signInManager.SignInAsync(newUser, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }

        // Provider did not supply an email — ask the user
        ViewData["ReturnUrl"]     = returnUrl;
        ViewData["LoginProvider"] = info.LoginProvider;
        return View("ExternalLoginConfirmation", new ExternalLoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalLoginConfirmation(
        ExternalLoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return RedirectToAction(nameof(Login));

        var user = new ApplicationUser
        {
            UserName       = model.Email,
            Email          = model.Email,
            EmailConfirmed = true,
            FirstName      = model.FirstName ?? "",
            LastName       = model.LastName  ?? ""
        };

        var result = await _userManager.CreateAsync(user);
        if (result.Succeeded)
        {
            await _userManager.AddLoginAsync(user, info);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToLocal(returnUrl);
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        ViewData["ReturnUrl"] = returnUrl;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User);
        var email  = User.Identity?.Name;
        await _signInManager.SignOutAsync();
        await _log.LogAsync(AppLogLevel.Info, "Account", $"Logout: {email}", userId: userId);
        return RedirectToAction("Index", "Home");
    }

    // ── Email verification ────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult VerifyEmailSent()
    {
        // Show the "check your inbox" page regardless of auth state
        return View();
    }

    [HttpGet("account/confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            return View("ConfirmEmailResult", false);

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return View("ConfirmEmailResult", false);

        var result = await _userManager.ConfirmEmailAsync(user, token);

        if (result.Succeeded)
        {
            await _log.LogAsync(AppLogLevel.Info, "Account",
                $"Email confirmed: {user.Email}", userId: user.Id);

            _ = Task.Run(async () =>
            {
                try { await _userEmail.SendWelcomeAsync(user); }
                catch { /* best-effort */ }
            });

            return View("ConfirmEmailResult", true);
        }

        // Token already consumed but email was confirmed on a previous click — show success
        if (user.EmailConfirmed)
            return View("ConfirmEmailResult", true);

        return View("ConfirmEmailResult", false);
    }

    [HttpPost("account/resend-verification")]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> ResendVerification()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (user.EmailConfirmed)
        {
            TempData["VerifyInfo"] = "already";
            return RedirectToAction(nameof(VerifyEmailSent));
        }

        var verifyToken  = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmUrl   = Url.Action(nameof(ConfirmEmail), "Account",
            new { userId = user.Id, token = verifyToken }, Request.Scheme) ?? "";

        _ = Task.Run(async () =>
        {
            try { await SendVerificationEmailAsync(user, confirmUrl); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resend verification email to {Email}", user.Email);
            }
        });

        TempData["VerifyInfo"] = "resent";
        return RedirectToAction(nameof(VerifyEmailSent));
    }

    // ── Forgot / Reset password ─────────────────────────────────────────────

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        // Always redirect to confirmation to avoid email enumeration
        if (user is null || !await _userManager.IsEmailConfirmedAsync(user))
            return RedirectToAction(nameof(ForgotPasswordConfirmation));

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = Url.Action(nameof(ResetPassword), "Account",
            new { email = user.Email, token }, Request.Scheme) ?? "";

        await SendPasswordResetEmailAsync(user, resetUrl);
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest("A reset token is required.");

        var model = new ResetPasswordViewModel { Email = email ?? "", Token = token };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
            return RedirectToAction(nameof(ResetPasswordConfirmation));

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded)
        {
            await _log.LogAsync(AppLogLevel.Info, "Account",
                $"Password reset: {model.Email}", userId: user.Id);
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    // ── Email helpers ────────────────────────────────────────────────────────

    private async Task SendPasswordResetEmailAsync(ApplicationUser user, string resetUrl)
    {
        var fromName = _config["Email:FromName"] ?? "frontida4all";
        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#4f46e5">Επαναφορά κωδικού — {fromName}</h2>
              <p>Γεια σου {System.Net.WebUtility.HtmlEncode(user.FirstName)},</p>
              <p>Λάβαμε αίτημα για επαναφορά του κωδικού σου. Πάτα το παρακάτω κουμπί:</p>
              <p style="margin:32px 0">
                <a href="{resetUrl}"
                   style="background:#4f46e5;color:#fff;padding:12px 28px;border-radius:6px;
                          text-decoration:none;font-weight:600;font-size:15px">
                  Επαναφορά Κωδικού
                </a>
              </p>
              <p style="color:#6b7280;font-size:13px">
                Αν δεν ζήτησες επαναφορά κωδικού, αγνόησε αυτό το μήνυμα.<br/>
                Ο σύνδεσμος λήγει σύντομα.
              </p>
              <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0"/>
              <p style="color:#9ca3af;font-size:12px">{fromName} · Greece</p>
            </div>
            """;

        await _email.SendAsync(user.Email!, "Επαναφορά κωδικού", html);
    }

    private async Task SendVerificationEmailAsync(ApplicationUser user, string confirmUrl)
    {

        var fromName = _config["Email:FromName"] ?? "frontida4all";
        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#4f46e5">Επιβεβαίωση email — {fromName}</h2>
              <p>Γεια σου {user.FirstName},</p>
              <p>Ευχαριστούμε για την εγγραφή σου! Πάτα το παρακάτω κουμπί για να επιβεβαιώσεις
                 τη διεύθυνση email σου:</p>
              <p style="margin:32px 0">
                <a href="{confirmUrl}"
                   style="background:#4f46e5;color:#fff;padding:12px 28px;border-radius:6px;
                          text-decoration:none;font-weight:600;font-size:15px">
                  Επιβεβαίωση Email
                </a>
              </p>
              <p style="color:#6b7280;font-size:13px">
                Αν δεν έκανες εγγραφή, αγνόησε αυτό το μήνυμα.<br/>
                Ο σύνδεσμος λήγει μετά από 24 ώρες.
              </p>
              <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0"/>
              <p style="color:#9ca3af;font-size:12px">{fromName} · Greece</p>
            </div>
            """;

        await _email.SendAsync(user.Email!, "Επιβεβαίωση email", html);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }
}
