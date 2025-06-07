namespace NexusPoint.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string HashedPassword { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
    }
}
