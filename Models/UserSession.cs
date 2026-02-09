namespace AS_Assignment2.Models
{
    public class UserSession
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public string SessionId { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public string? IpAddress { get; set; }
        public bool IsActive { get; set; }
    }
}
