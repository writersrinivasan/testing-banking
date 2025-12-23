namespace BankingAuth.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LastLoginAttempt { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
