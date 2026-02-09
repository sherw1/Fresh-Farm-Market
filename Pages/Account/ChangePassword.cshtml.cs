using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AS_Assignment2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AS_Assignment2.Pages.Account
{
    [Authorize]
    public class ChangePasswordModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<Member> _passwordHasher;

        public ChangePasswordModel(AppDbContext db)
        {
            _db = db;
            _passwordHasher = new PasswordHasher<Member>();
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            public string CurrentPassword { get; set; }

            [Required]
            [MinLength(12)]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_+=\-{}\[\]:;""'<>,.?/]).{12,}$", 
                ErrorMessage = "Password must be at least 12 characters and include upper, lower, number, and special.")]
            [DataType(DataType.Password)]
            public string NewPassword { get; set; }

            [Required]
            [Compare("NewPassword")]
            [DataType(DataType.Password)]
            public string ConfirmNewPassword { get; set; }
        }

        public void OnGet()
        {
            // Prevent caching
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Prevent caching
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            if (!ModelState.IsValid)
            {
                return Page();
            }

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

            // Verify current password
            var verifyResult = _passwordHasher.VerifyHashedPassword(member, member.PasswordHash, Input.CurrentPassword);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                Message = "Current password is incorrect.";
                IsSuccess = false;
                return Page();
            }

            // Check minimum password age (cannot change within 5 minutes of last change)
            if (member.LastPasswordChangeDate.HasValue)
            {
                var minAge = TimeSpan.FromMinutes(5);
                var timeSinceLastChange = DateTime.UtcNow - member.LastPasswordChangeDate.Value;
                if (timeSinceLastChange < minAge)
                {
                    var remainingTime = minAge - timeSinceLastChange;
                    Message = $"Password cannot be changed yet. Please wait {remainingTime.Minutes} minutes and {remainingTime.Seconds} seconds.";
                    IsSuccess = false;
                    return Page();
                }
            }

            // Check password history (prevent reuse of last 2 passwords)
            var newPasswordHash = _passwordHasher.HashPassword(member, Input.NewPassword);
            
            if (!string.IsNullOrEmpty(member.PreviousPasswordHash1))
            {
                var hash1Match = _passwordHasher.VerifyHashedPassword(member, member.PreviousPasswordHash1, Input.NewPassword);
                if (hash1Match == PasswordVerificationResult.Success)
                {
                    Message = "Cannot reuse previous password. Please choose a different password.";
                    IsSuccess = false;
                    return Page();
                }
            }

            if (!string.IsNullOrEmpty(member.PreviousPasswordHash2))
            {
                var hash2Match = _passwordHasher.VerifyHashedPassword(member, member.PreviousPasswordHash2, Input.NewPassword);
                if (hash2Match == PasswordVerificationResult.Success)
                {
                    Message = "Cannot reuse previous password. Please choose a different password.";
                    IsSuccess = false;
                    return Page();
                }
            }

            // Update password history
            member.PreviousPasswordHash2 = member.PreviousPasswordHash1;
            member.PreviousPasswordHash1 = member.PasswordHash;
            member.PasswordHash = newPasswordHash;
            member.LastPasswordChangeDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Log audit
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var log = new AuditLog
            {
                Email = member.Email,
                Action = "PasswordChange",
                Timestamp = DateTime.UtcNow,
                IsSuccess = true,
                IpAddress = ipAddress,
                Details = "Password changed successfully"
            };
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();

            Message = "Password changed successfully!";
            IsSuccess = true;
            return Page();
        }
    }
}
