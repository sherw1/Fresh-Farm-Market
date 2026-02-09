using System.Text.Json;

namespace AS_Assignment2.Services
{
    public class ReCaptchaService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public ReCaptchaService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<bool> VerifyTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            var secretKey = _configuration["ReCaptcha:SecretKey"];
            var url = $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={token}";

            try
            {
                var response = await _httpClient.PostAsync(url, null);
                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ReCaptchaResponse>(jsonString);

                return result?.success == true && result.score >= 0.5;
            }
            catch
            {
                return false;
            }
        }

        private class ReCaptchaResponse
        {
            public bool success { get; set; }
            public double score { get; set; }
            public string action { get; set; }
            public DateTime challenge_ts { get; set; }
            public string hostname { get; set; }
        }
    }
}
