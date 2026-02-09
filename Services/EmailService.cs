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
                // Log with sanitized email information only
                _logger.LogError(ex, "Failed to send 2FA code");
                Console.WriteLine($"==========================================");
                Console.WriteLine($"?? TWO-FACTOR AUTHENTICATION CODE");
                Console.WriteLine($"Code: {code}");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"==========================================");
                return false;
            }
        }

        public async Task<bool> SendPasswordResetLinkAsync(string email, string resetLink)
        {
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
                // Log with sanitized information only
                _logger.LogError(ex, "Failed to send password reset link");
                Console.WriteLine($"==========================================");
                Console.WriteLine($"PASSWORD RESET LINK SENT");
                Console.WriteLine($"Link: {resetLink}");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"==========================================");
                return false;
            }
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
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
                _logger.LogWarning("Email settings not configured");
                Console.WriteLine($"?? Email configuration missing");
                Console.WriteLine($"Subject: {subject}");
                return false;
            }

            try
            {
                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    Timeout = 10000
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

                _logger.LogInformation("Email sent successfully");
                Console.WriteLine($"? Email sent successfully");
                return true;
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "SMTP error sending email");
                Console.WriteLine($"? SMTP Error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email");
                Console.WriteLine($"? Email Error: {ex.Message}");
                throw;
            }
        }
    }
}
