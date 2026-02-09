using System.ComponentModel.DataAnnotations;

namespace AS_Assignment2.Models
{
    public class Member
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        // Stored encrypted in database
        [Required]
        [StringLength(256)]
        public string EncryptedCreditCard { get; set; }

        [Required]
        [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Invalid gender.")]
        public string Gender { get; set; }

        [Required]
        [Phone]
        [StringLength(20)]
        public string MobileNo { get; set; }

        [Required]
        [StringLength(200)]
        public string DeliveryAddress { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; }

        // Password stored as hash
        [Required]
        [StringLength(512)]
        public string PasswordHash { get; set; }

        // .JPG only will be validated at upload; store path/filename
        [StringLength(260)]
        public string? PhotoPath { get; set; }

        // Allow all special chars; limit length
        [StringLength(1000)]
        public string? AboutMe { get; set; }

        // Account lockout
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEndTime { get; set; }

        // Password history
        public string? PreviousPasswordHash1 { get; set; }
        public string? PreviousPasswordHash2 { get; set; }

        // Password age tracking
        public DateTime? LastPasswordChangeDate { get; set; }

        // 2FA fields
        public bool TwoFactorEnabled { get; set; }
        public string? TwoFactorCode { get; set; }
        public DateTime? TwoFactorCodeExpiry { get; set; }
    }
}
