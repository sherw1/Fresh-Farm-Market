using System.Security.Claims;
using AS_Assignment2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AS_Assignment2.Pages
{
    [Authorize]
    public class HomeModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public Member? Member { get; set; }
        public string? DecryptedCreditCard { get; set; }
        public bool PasswordExpired { get; set; }

        public HomeModel(AppDbContext db, IDataProtectionProvider dataProtectionProvider)
        {
            _db = db;
            _dataProtectionProvider = dataProtectionProvider;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Prevent caching of this page
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "-1";

            // FIRST: Check if user is authenticated at all
            if (User?.Identity?.IsAuthenticated != true)
            {
                HttpContext.Session.Clear();
                return RedirectToPage("/Account/Login");
            }

            // SECOND: Check session validity
            var sessionId = HttpContext.Session.GetString("SessionId");
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(userIdClaim))
            {
                // No session - force logout
                HttpContext.Session.Clear();
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToPage("/Account/Login");
            }

            // THIRD: Verify session is still active in database
            var userSession = await _db.UserSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);

            if (userSession == null)
            {
                // Session not found or inactive - force logout
                HttpContext.Session.Clear();
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToPage("/Account/Login");
            }

            // FOURTH: Check session timeout (20 minutes)
            var sessionAge = DateTime.UtcNow - userSession.LastActivityTime;
            if (sessionAge.TotalMinutes > 20)
            {
                // Session expired - deactivate and force logout
                var sessionToDeactivate = await _db.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                if (sessionToDeactivate != null)
                {
                    sessionToDeactivate.IsActive = false;
                    await _db.SaveChangesAsync();
                }
                
                HttpContext.Session.Clear();
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToPage("/Account/Login");
            }

            // Update last activity time
            var activeSession = await _db.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
            if (activeSession != null)
            {
                activeSession.LastActivityTime = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // Get member data
            var memberId = int.Parse(userIdClaim);
            Member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId);

            if (Member == null)
            {
                // Member not found - force logout
                HttpContext.Session.Clear();
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToPage("/Account/Login");
            }

            // Check password age (90 days maximum)
            if (Member.LastPasswordChangeDate.HasValue)
            {
                var passwordAge = DateTime.UtcNow - Member.LastPasswordChangeDate.Value;
                if (passwordAge.TotalDays > 90)
                {
                    PasswordExpired = true;
                }
            }

            // Decrypt credit card if exists
            if (!string.IsNullOrEmpty(Member.EncryptedCreditCard))
            {
                var protector = _dataProtectionProvider.CreateProtector("member-credit-card");
                try
                {
                    DecryptedCreditCard = protector.Unprotect(Member.EncryptedCreditCard);
                }
                catch
                {
                    DecryptedCreditCard = "Unable to decrypt";
                }
            }

            return Page();
        }
    }
}
