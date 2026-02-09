using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AS_Assignment2.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AS_Assignment2.Pages.Account
{
    public class TwoFactorModel : PageModel
    {
        private readonly AppDbContext _db;

        public TwoFactorModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ErrorMessage { get; set; }
        public string DemoCode { get; set; } // For demo display

        public class InputModel
        {
            [Required]
            [StringLength(6, MinimumLength = 6)]
            public string Code { get; set; }
        }

        public void OnGet()
        {
            var email = TempData["PendingEmail"]?.ToString();
            if (string.IsNullOrEmpty(email))
            {
                Response.Redirect("/Account/Login");
                return;
            }
            
            // Get demo code from TempData
            DemoCode = TempData["TwoFactorCode"]?.ToString();
            
            // Keep data for POST
            TempData.Keep("PendingEmail");
            TempData.Keep("PendingMemberId");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var email = TempData["PendingEmail"]?.ToString();
            var memberId = TempData["PendingMemberId"]?.ToString();
            
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(memberId))
            {
                return RedirectToPage("/Account/Login");
            }

            var member = await _db.Members.FindAsync(int.Parse(memberId));
            if (member == null)
            {
                return RedirectToPage("/Account/Login");
            }

            // Verify code
            if (member.TwoFactorCode != Input.Code || 
                !member.TwoFactorCodeExpiry.HasValue || 
                member.TwoFactorCodeExpiry.Value < DateTime.UtcNow)
            {
                ErrorMessage = "Invalid or expired verification code.";
                TempData["PendingEmail"] = email;
                TempData["PendingMemberId"] = memberId;
                return Page();
            }

            // Clear 2FA code
            member.TwoFactorCode = null;
            member.TwoFactorCodeExpiry = null;
            await _db.SaveChangesAsync();

            // Create session
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var sessionId = Guid.NewGuid().ToString();
            var userSession = new UserSession
            {
                MemberId = member.Id,
                SessionId = sessionId,
                LoginTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                IpAddress = ipAddress,
                IsActive = true
            };
            _db.UserSessions.Add(userSession);
            await _db.SaveChangesAsync();

            // Sign in
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, member.Id.ToString()),
                new Claim(ClaimTypes.Email, member.Email),
                new Claim(ClaimTypes.Name, member.FullName),
                new Claim("SessionId", sessionId)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1)
                });

            HttpContext.Session.SetString("UserId", member.Id.ToString());
            HttpContext.Session.SetString("SessionId", sessionId);

            // Log audit
            var log = new AuditLog
            {
                Email = member.Email,
                Action = "2FA Success",
                Timestamp = DateTime.UtcNow,
                IsSuccess = true,
                IpAddress = ipAddress,
                Details = "Two-factor authentication successful"
            };
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();

            return RedirectToPage("/Home");
        }
    }
}
