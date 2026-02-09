using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AS_Assignment2.Models;
using AS_Assignment2.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AS_Assignment2.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly IPasswordHasher<Member> _passwordHasher;
        private readonly ReCaptchaService _reCaptchaService;

        public RegisterModel(AppDbContext db, IDataProtectionProvider dataProtectionProvider, ReCaptchaService reCaptchaService)
        {
            _db = db;
            _dataProtectionProvider = dataProtectionProvider;
            _passwordHasher = new PasswordHasher<Member>();
            _reCaptchaService = reCaptchaService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public string FullName { get; set; }

            [Required]
            [CreditCard]
            public string CreditCard { get; set; }

            [Required]
            public string Gender { get; set; }

            [Required]
            [Phone]
            public string MobileNo { get; set; }

            [Required]
            public string DeliveryAddress { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [MinLength(12)]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_+=\-{}\[\]:;""'<>,.?/]).{12,}$", ErrorMessage = "Password must be at least 12 characters and include upper, lower, number, and special.")]
            public string Password { get; set; }

            [Required]
            [Compare("Password")] 
            public string ConfirmPassword { get; set; }

            [DataType(DataType.Upload)]
            public IFormFile? Photo { get; set; }

            public string? AboutMe { get; set; }

            public string? RecaptchaToken { get; set; }
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            // Verify reCAPTCHA
            if (!await _reCaptchaService.VerifyTokenAsync(Input.RecaptchaToken))
            {
                ModelState.AddModelError(string.Empty, "reCAPTCHA validation failed. Please try again.");
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Check duplicate email
            var exists = _db.Members.Any(m => m.Email == Input.Email);
            if (exists)
            {
                ModelState.AddModelError(string.Empty, "Email already registered.");
                return Page();
            }

            // Encrypt credit card
            var protector = _dataProtectionProvider.CreateProtector("member-credit-card");
            var encryptedCc = protector.Protect(Input.CreditCard);

            var member = new Member
            {
                FullName = Input.FullName,
                EncryptedCreditCard = encryptedCc,
                Gender = Input.Gender,
                MobileNo = Input.MobileNo,
                DeliveryAddress = Input.DeliveryAddress,
                Email = Input.Email,
                AboutMe = Input.AboutMe,
                FailedLoginAttempts = 0,
                LastPasswordChangeDate = DateTime.UtcNow,
                TwoFactorEnabled = false // Disable 2FA for new users by default
            };

            // Hash password
            member.PasswordHash = _passwordHasher.HashPassword(member, Input.Password);

            // Save photo only if .jpg
            if (Input.Photo != null && Input.Photo.Length > 0)
            {
                var ext = Path.GetExtension(Input.Photo.FileName).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg")
                {
                    ModelState.AddModelError("Input.Photo", "Only .JPG images are allowed.");
                    return Page();
                }

                var uploads = Path.Combine("wwwroot", "uploads");
                Directory.CreateDirectory(uploads);
                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploads, fileName);
                using var stream = System.IO.File.Create(filePath);
                await Input.Photo.CopyToAsync(stream);
                member.PhotoPath = $"/uploads/{fileName}";
            }

            _db.Members.Add(member);
            await _db.SaveChangesAsync();

            // Log audit for registration
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var registrationLog = new AuditLog
            {
                Email = Input.Email,
                Action = "Registration",
                Timestamp = DateTime.UtcNow,
                IsSuccess = true,
                IpAddress = ipAddress,
                Details = "New member registered"
            };
            _db.AuditLogs.Add(registrationLog);
            await _db.SaveChangesAsync();

            // Automatically log in the user after successful registration
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

            // Create authentication claims
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
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20)
                });

            HttpContext.Session.SetString("UserId", member.Id.ToString());
            HttpContext.Session.SetString("SessionId", sessionId);

            // Log automatic login after registration
            var loginLog = new AuditLog
            {
                Email = Input.Email,
                Action = "AutoLogin",
                Timestamp = DateTime.UtcNow,
                IsSuccess = true,
                IpAddress = ipAddress,
                Details = "Automatic login after registration"
            };
            _db.AuditLogs.Add(loginLog);
            await _db.SaveChangesAsync();

            // Redirect to Home page
            return RedirectToPage("/Home");
        }
    }
}
