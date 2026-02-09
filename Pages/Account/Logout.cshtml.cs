using AS_Assignment2.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AS_Assignment2.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(AppDbContext db, ILogger<LogoutModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var sessionId = HttpContext.Session.GetString("SessionId");
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                _logger.LogInformation("Logout requested");

                // Deactivate session in database
                if (!string.IsNullOrEmpty(sessionId))
                {
                    var userSession = await _db.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                    if (userSession != null)
                    {
                        userSession.IsActive = false;
                        
                        // Log audit without email exposure
                        var log = new AuditLog
                        {
                            Email = await GetUserEmailAsync(userSession.MemberId),
                            Action = "Logout",
                            Timestamp = DateTime.UtcNow,
                            IsSuccess = true,
                            IpAddress = ipAddress,
                            Details = "User logged out"
                        };
                        _db.AuditLogs.Add(log);
                        
                        await _db.SaveChangesAsync();
                        _logger.LogInformation("Session deactivated");
                    }
                }

                // Sign out FIRST (before clearing session)
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Clear all session data
                HttpContext.Session.Clear();

                // Add cache control headers to prevent back button access
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "-1";

                // Clear any response cookies
                Response.Cookies.Delete(".AspNetCore.Session");
                Response.Cookies.Delete(".AspNetCore.Cookies");
                Response.Cookies.Delete(".AspNetCore.Antiforgery");

                _logger.LogInformation("Logout completed, redirecting to login");

                return RedirectToPage("/Account/Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return RedirectToPage("/Account/Login");
            }
        }
        
        private async Task<string> GetUserEmailAsync(int memberId)
        {
            var member = await _db.Members.FindAsync(memberId);
            return member?.Email ?? "Unknown";
        }
    }
}
