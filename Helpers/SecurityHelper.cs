using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace AS_Assignment2.Helpers
{
    public static class SecurityHelper
    {
        /// <summary>
        /// Sanitize input to prevent XSS attacks
        /// </summary>
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // HTML encode the input
            return HtmlEncoder.Default.Encode(input);
        }

        /// <summary>
        /// Validate email format
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate phone number format
        /// </summary>
        public static bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            var regex = new Regex(@"^\+?[\d\s\-\(\)]+$");
            return regex.IsMatch(phone);
        }

        /// <summary>
        /// Check password strength
        /// </summary>
        public static (bool IsStrong, List<string> Issues) CheckPasswordStrength(string password)
        {
            var issues = new List<string>();

            if (password.Length < 12)
                issues.Add("Password must be at least 12 characters long");

            if (!Regex.IsMatch(password, @"[a-z]"))
                issues.Add("Password must contain at least one lowercase letter");

            if (!Regex.IsMatch(password, @"[A-Z]"))
                issues.Add("Password must contain at least one uppercase letter");

            if (!Regex.IsMatch(password, @"\d"))
                issues.Add("Password must contain at least one digit");

            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+=\-{}\[\]:;""'<>,.?/]"))
                issues.Add("Password must contain at least one special character");

            return (issues.Count == 0, issues);
        }

        /// <summary>
        /// Validate file extension for image upload
        /// </summary>
        public static bool IsValidImageExtension(string fileName, string[] allowedExtensions)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }
    }
}
