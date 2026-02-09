using System.ComponentModel.DataAnnotations;

namespace AS_Assignment2.Models
{
    public class LoginAttempt
    {
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string Email { get; set; }

        public DateTime AttemptTime { get; set; }

        public bool IsSuccess { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }
    }
}
