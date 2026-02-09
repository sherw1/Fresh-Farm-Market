using System.ComponentModel.DataAnnotations;
using AS_Assignment2.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AS_Assignment2.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<Member> _passwordHasher;

        public ResetPasswordModel(AppDbContext db)
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
            public string Token { get; set; }

            [Required]
            [MinLength(12)]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_+=\-{}\[\]:;""'<>,.?/]).{12,}$",
                ErrorMessage = "Password must be at least 12 characters and include upper, lower, number, and special.")]
            [DataType(DataType.Password)]
            public string NewPassword { get; set; }

            [Required]
            [Compare("NewPassword")]
            [DataType(DataType.Password)]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Message = "Invalid reset token.";
                IsSuccess = false;
                return Page();
            }

            Input.Token = token;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var member = await _db.Members.FirstOrDefaultAsync(m => 
                m.TwoFactorCode == Input.Token && 
                m.TwoFactorCodeExpiry.HasValue && 
                m.TwoFactorCodeExpiry.Value > DateTime.UtcNow);

            if (member == null)
            {
                Message = "Invalid or expired reset token.";
                IsSuccess = false;
                return Page();
            }

            // Check password history
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

            // Update password
            member.PreviousPasswordHash2 = member.PreviousPasswordHash1;
            member.PreviousPasswordHash1 = member.PasswordHash;
            member.PasswordHash = _passwordHasher.HashPassword(member, Input.NewPassword);
            member.LastPasswordChangeDate = DateTime.UtcNow;
            member.TwoFactorCode = null;
            member.TwoFactorCodeExpiry = null;

            await _db.SaveChangesAsync();

            // Log audit
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var log = new AuditLog
            {
                Email = member.Email,
                Action = "Password Reset",
                Timestamp = DateTime.UtcNow,
                IsSuccess = true,
                IpAddress = ipAddress,
                Details = "Password reset successfully"
            };
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();

            Message = "Your password has been reset successfully!";
            IsSuccess = true;
            return Page();
        }
    }
}
