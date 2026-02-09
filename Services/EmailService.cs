namespace AS_Assignment2.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendTwoFactorCodeAsync(string email, string code)
        {
            // In production, implement actual email sending using SendGrid, SMTP, etc.
            // For now, log the code (for demo purposes)
            Console.WriteLine($"2FA Code for {email}: {code}");
            
            // Simulate async operation
            await Task.CompletedTask;
            return true;
        }

        public async Task<bool> SendPasswordResetLinkAsync(string email, string resetLink)
        {
            // In production, implement actual email sending
            Console.WriteLine($"Password Reset Link for {email}: {resetLink}");
            
            await Task.CompletedTask;
            return true;
        }
    }
}
