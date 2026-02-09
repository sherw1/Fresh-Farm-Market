using System.ComponentModel.DataAnnotations;
using AS_Assignment2.Models;
using AS_Assignment2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AS_Assignment2.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly EmailService _emailService;

        public ForgotPasswordModel(AppDbContext db, EmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == Input.Email);
            
            // Always show success message (security best practice - don't reveal if email exists)
            if (member != null)
            {
                // Generate reset token
                var resetToken = Guid.NewGuid().ToString();
                member.TwoFactorCode = resetToken; // Reuse field for reset token
                member.TwoFactorCodeExpiry = DateTime.UtcNow.AddMinutes(1); // ? Changed from AddHours(1) to AddMinutes(1)
                await _db.SaveChangesAsync();

                // Create reset link
                var resetLink = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?token={resetToken}";
                
                // Log to console/terminal for demo
                Console.WriteLine($"==========================================");
                Console.WriteLine($"PASSWORD RESET LINK FOR: {member.Email}");
                Console.WriteLine($"Link: {resetLink}");
                Console.WriteLine($"Token: {resetToken}");
                Console.WriteLine($"Expires: {member.TwoFactorCodeExpiry:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"==========================================");
                
                await _emailService.SendPasswordResetLinkAsync(member.Email, resetLink);

                // Log audit
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var log = new AuditLog
                {
                    Email = Input.Email,
                    Action = "Password Reset Requested",
                    Timestamp = DateTime.UtcNow,
                    IsSuccess = true,
                    IpAddress = ipAddress,
                    Details = "Password reset link sent"
                };
                _db.AuditLogs.Add(log);
                await _db.SaveChangesAsync();
            }

            Message = "If an account exists with that email, a password reset link has been sent. Check your console/terminal for the link.";
            IsSuccess = true;
            return Page();
        }
    }
}
