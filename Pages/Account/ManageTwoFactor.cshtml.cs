using System.Security.Claims;
using AS_Assignment2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AS_Assignment2.Pages.Account
{
    [Authorize]
    public class ManageTwoFactorModel : PageModel
    {
        private readonly AppDbContext _db;

        public ManageTwoFactorModel(AppDbContext db)
        {
            _db = db;
        }

        public bool TwoFactorEnabled { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login");
            }

            var member = await _db.Members.FindAsync(int.Parse(userId));
            if (member == null)
            {
                return RedirectToPage("/Account/Login");
            }

            TwoFactorEnabled = member.TwoFactorEnabled;
            return Page();
        }

        public async Task<IActionResult> OnPostEnableAsync()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login");
            }

            var member = await _db.Members.FindAsync(int.Parse(userId));
            if (member == null)
            {
                return RedirectToPage("/Account/Login");
            }

            member.TwoFactorEnabled = true;
            await _db.SaveChangesAsync();

            // Log audit
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var log = new AuditLog
            {
                Email = member.Email,
                Action = "2FA Enabled",
                Timestamp = DateTime.UtcNow,
                IsSuccess = true,
                IpAddress = ipAddress,
                Details = "Two-factor authentication enabled"
            };
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();

            Message = "Two-Factor Authentication has been enabled for your account. You will need to enter a verification code when you login.";
            IsSuccess = true;
            TwoFactorEnabled = true;
            return Page();
        }

        public async Task<IActionResult> OnPostDisableAsync()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login");
            }

            var member = await _db.Members.FindAsync(int.Parse(userId));
            if (member == null)
            {
                return RedirectToPage("/Account/Login");
            }

            member.TwoFactorEnabled = false;
            member.TwoFactorCode = null;
            member.TwoFactorCodeExpiry = null;
            await _db.SaveChangesAsync();

            // Log audit
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var log = new AuditLog
            {
                Email = member.Email,
                Action = "2FA Disabled",
                Timestamp = DateTime.UtcNow,
                IsSuccess = true,
                IpAddress = ipAddress,
                Details = "Two-factor authentication disabled"
            };
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();

            Message = "Two-Factor Authentication has been disabled for your account.";
            IsSuccess = true;
            TwoFactorEnabled = false;
            return Page();
        }
    }
}
