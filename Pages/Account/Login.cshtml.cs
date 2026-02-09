using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AS_Assignment2.Models;
using AS_Assignment2.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AS_Assignment2.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<Member> _passwordHasher;
        private readonly EmailService _emailService;

        public LoginModel(AppDbContext db, EmailService emailService)
        {
            _db = db;
            _passwordHasher = new PasswordHasher<Member>();
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            public string Password { get; set; }
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
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Check if account is locked
            if (member != null && member.LockoutEndTime.HasValue && member.LockoutEndTime.Value > DateTime.UtcNow)
            {
                ErrorMessage = $"Account is locked until {member.LockoutEndTime.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}. Please try again later.";
                await LogAuditAsync(Input.Email, "Login", false, ipAddress, "Account locked");
                return Page();
            }

            // Verify password
            if (member == null || _passwordHasher.VerifyHashedPassword(member, member.PasswordHash, Input.Password) == PasswordVerificationResult.Failed)
            {
                // Log failed attempt
                await LogLoginAttemptAsync(Input.Email, false, ipAddress);
                await LogAuditAsync(Input.Email, "Login", false, ipAddress, "Invalid credentials");

                if (member != null)
                {
                    member.FailedLoginAttempts++;

                    // Lock account after 3 failed attempts
                    if (member.FailedLoginAttempts >= 3)
                    {
                        member.LockoutEndTime = DateTime.UtcNow.AddMinutes(1); // ? Changed from 15 to 1 minute
                        ErrorMessage = "Account locked for 1 minute due to multiple failed login attempts."; // ? Updated message
                    }
                    else
                    {
                        ErrorMessage = $"Invalid email or password. Attempts remaining: {3 - member.FailedLoginAttempts}";
                    }

                    await _db.SaveChangesAsync();
                }
                else
                {
                    ErrorMessage = "Invalid email or password.";
                }

                return Page();
            }

            // Check for concurrent sessions
            var existingSessions = await _db.UserSessions
                .Where(s => s.MemberId == member.Id && s.IsActive)
                .ToListAsync();

            if (existingSessions.Any())
            {
                // Deactivate old sessions (single session per user)
                foreach (var session in existingSessions)
                {
                    session.IsActive = false;
                }
                await _db.SaveChangesAsync();
                await LogAuditAsync(Input.Email, "Login", true, ipAddress, "Previous sessions terminated");
            }

            // Reset failed attempts and unlock
            member.FailedLoginAttempts = 0;
            member.LockoutEndTime = null;

            // Log successful attempt
            await LogLoginAttemptAsync(Input.Email, true, ipAddress);

            // Check if 2FA is enabled
            if (member.TwoFactorEnabled)
            {
                // Generate 6-digit code
                var code = new Random().Next(100000, 999999).ToString();
                member.TwoFactorCode = code;
                member.TwoFactorCodeExpiry = DateTime.UtcNow.AddMinutes(1); // ? Changed from 10 to 1 minute
                await _db.SaveChangesAsync();

                // Enhanced console logging for demo
                Console.WriteLine($"==========================================");
                Console.WriteLine($"?? TWO-FACTOR AUTHENTICATION CODE");
                Console.WriteLine($"User: {member.Email}");
                Console.WriteLine($"Code: {code}");
                Console.WriteLine($"Expires: {member.TwoFactorCodeExpiry:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"==========================================");

                // Send code via email
                await _emailService.SendTwoFactorCodeAsync(member.Email, code);

                // Store member info AND code in TempData for 2FA page (demo only)
                TempData["PendingEmail"] = member.Email;
                TempData["PendingMemberId"] = member.Id.ToString();
                TempData["TwoFactorCode"] = code; // For demo display

                await LogAuditAsync(Input.Email, "2FA Code Sent", true, ipAddress, "Two-factor code generated");

                return RedirectToPage("/Account/TwoFactor");
            }

            // If no 2FA, proceed with normal login
            await CompleteLoginAsync(member, ipAddress);

            return RedirectToPage("/Home");
        }

        private async Task CompleteLoginAsync(Member member, string ipAddress)
        {
            // Create new session record
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

            // Sign in with cookie authentication
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
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1) // ? Changed from 20 to 1 minute
                });

            HttpContext.Session.SetString("UserId", member.Id.ToString());
            HttpContext.Session.SetString("SessionId", sessionId);

            await LogAuditAsync(member.Email, "Login", true, ipAddress, "Successful login");
        }

        private async Task LogLoginAttemptAsync(string email, bool isSuccess, string ipAddress)
        {
            var attempt = new LoginAttempt
            {
                Email = email,
                AttemptTime = DateTime.UtcNow,
                IsSuccess = isSuccess,
                IpAddress = ipAddress
            };
            _db.LoginAttempts.Add(attempt);
            await _db.SaveChangesAsync();
        }

        private async Task LogAuditAsync(string email, string action, bool isSuccess, string ipAddress, string details)
        {
            var log = new AuditLog
            {
                Email = email,
                Action = action,
                Timestamp = DateTime.UtcNow,
                IsSuccess = isSuccess,
                IpAddress = ipAddress,
                Details = details
            };
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
        }
    }
}
