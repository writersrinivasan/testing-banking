namespace BankingAuth.Models
{
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? ErrorMessage { get; set; }
        public bool RequiresTwoFactor { get; set; }
        public DateTime TokenExpiry { get; set; }
    }
}
