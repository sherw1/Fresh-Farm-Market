using System.Net;
using System.Net.Mail;

namespace AS_Assignment2.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendTwoFactorCodeAsync(string email, string code)
        {
            var maskedEmail = MaskEmail(email); // Mask once at the start
            
            try
            {
                var subject = "Your Two-Factor Authentication Code";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                            <h2 style='color: #007bff;'>?? Two-Factor Authentication</h2>
                            <p>Hello,</p>
                            <p>You are attempting to login to your Fresh Farm Market account.</p>
                            <p>Your verification code is:</p>
                            <div style='background-color: #f8f9fa; padding: 20px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                                <h1 style='color: #007bff; letter-spacing: 10px; font-family: monospace;'>{code}</h1>
                            </div>
                            <p><strong>This code will expire in 1 minute.</strong></p>
                            <p>If you did not attempt to login, please ignore this email and contact support immediately.</p>
                            <hr style='margin: 20px 0; border: none; border-top: 1px solid #ddd;'>
                            <p style='color: #6c757d; font-size: 12px;'>
                                This is an automated email. Please do not reply to this message.
                            </p>
                        </div>
                    </body>
                    </html>
                ";

                return await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send 2FA code to {maskedEmail}");
                // Still log to console as backup
                Console.WriteLine($"==========================================");
                Console.WriteLine($"?? TWO-FACTOR AUTHENTICATION CODE");
                Console.WriteLine($"User: {maskedEmail}");
                Console.WriteLine($"Code: {code}");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"==========================================");
                return false;
            }
        }

        public async Task<bool> SendPasswordResetLinkAsync(string email, string resetLink)
        {
            var maskedEmail = MaskEmail(email); // Mask once at the start
            
            try
            {
                var subject = "Reset Your Password - Fresh Farm Market";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                            <h2 style='color: #007bff;'>?? Password Reset Request</h2>
                            <p>Hello,</p>
                            <p>We received a request to reset your password for your Fresh Farm Market account.</p>
                            <p>Click the button below to reset your password:</p>
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{resetLink}' 
                                   style='background-color: #007bff; color: white; padding: 15px 30px; 
                                          text-decoration: none; border-radius: 5px; display: inline-block;
                                          font-weight: bold;'>
                                    Reset Password
                                </a>
                            </div>
                            <p>Or copy and paste this link into your browser:</p>
                            <p style='word-break: break-all; background-color: #f8f9fa; padding: 10px; border-radius: 3px;'>
                                {resetLink}
                            </p>
                            <p><strong>This link will expire in 1 minute.</strong></p>
                            <p>If you did not request a password reset, please ignore this email or contact support if you have concerns.</p>
                            <hr style='margin: 20px 0; border: none; border-top: 1px solid #ddd;'>
                            <p style='color: #6c757d; font-size: 12px;'>
                                This is an automated email. Please do not reply to this message.
                            </p>
                        </div>
                    </body>
                    </html>
                ";

                return await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send password reset link to {maskedEmail}");
                // Still log to console as backup
                Console.WriteLine($"==========================================");
                Console.WriteLine($"PASSWORD RESET LINK FOR: {maskedEmail}");
                Console.WriteLine($"Link: {resetLink}");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"==========================================");
                return false;
            }
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            var maskedToEmail = MaskEmail(toEmail); // Mask once at the start
            
            // Get email settings from configuration
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];
            var senderName = _configuration["EmailSettings:SenderName"] ?? "Fresh Farm Market";

            // Validate configuration
            if (string.IsNullOrEmpty(smtpServer) || 
                string.IsNullOrEmpty(senderEmail) || 
                string.IsNullOrEmpty(senderPassword))
            {
                _logger.LogWarning("Email settings not configured. Logging to console only.");
                Console.WriteLine($"?? Email would be sent to: {maskedToEmail}");
                Console.WriteLine($"Subject: {subject}");
                return false;
            }

            try
            {
                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    Timeout = 10000 // 10 seconds timeout
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);

                await smtpClient.SendMailAsync(message);

                _logger.LogInformation($"Email sent successfully to {maskedToEmail}");
                Console.WriteLine($"? Email sent successfully to {maskedToEmail}");
                return true;
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, $"SMTP error sending email to {maskedToEmail}");
                Console.WriteLine($"? SMTP Error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email to {maskedToEmail}");
                Console.WriteLine($"? Email Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Masks an email address for secure logging by hiding most of the local part
        /// while preserving the domain. This prevents exposure of sensitive user data
        /// in logs while maintaining enough information for debugging.
        /// </summary>
        /// <param name="email">The email address to mask</param>
        /// <returns>Masked email address (e.g., "j***n@example.com" for "john@example.com")</returns>
        private string MaskEmail(string email)
        {
            // Return as-is if null, empty, or doesn't contain @
            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            {
                return email ?? string.Empty;
            }

            try
            {
                var parts = email.Split('@');
                if (parts.Length != 2)
                {
                    return email; // Invalid format, return as-is
                }

                var localPart = parts[0];
                var domainPart = parts[1];

                // Mask the local part based on its length
                string maskedLocal;
                if (localPart.Length <= 1)
                {
                    // Single character: mask it completely
                    maskedLocal = "*";
                }
                else if (localPart.Length == 2)
                {
                    // Two characters: show first, mask second
                    maskedLocal = $"{localPart[0]}*";
                }
                else
                {
                    // Three or more characters: show first and last, mask middle
                    var middleLength = localPart.Length - 2;
                    var stars = new string('*', Math.Min(middleLength, 5)); // Max 5 stars for readability
                    maskedLocal = $"{localPart[0]}{stars}{localPart[^1]}";
                }

                return $"{maskedLocal}@{domainPart}";
            }
            catch
            {
                // If any error occurs during masking, return a generic masked format
                return "***@***";
            }
        }
    }
}
