using System.ComponentModel.DataAnnotations;

namespace AS_Assignment2.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string Email { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; }

        public DateTime Timestamp { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }

        public bool IsSuccess { get; set; }

        [StringLength(500)]
        public string? Details { get; set; }
    }
}
