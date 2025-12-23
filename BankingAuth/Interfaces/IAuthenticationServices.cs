namespace BankingAuth.Interfaces
{
    /// <summary>
    /// Interface for password hashing operations
    /// </summary>
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
    }

    /// <summary>
    /// Interface for token generation and validation
    /// </summary>
    public interface ITokenProvider
    {
        string GenerateToken(string username);
        bool ValidateToken(string token);
        string GetUsernameFromToken(string token);
        DateTime GetTokenExpiry(string token);
    }

    /// <summary>
    /// Interface for user data repository
    /// </summary>
    public interface IUserRepository
    {
        Task<Models.User> GetUserByUsernameAsync(string username);
        Task<bool> UserExistsAsync(string username);
        Task<bool> UpdateUserAsync(Models.User user);
    }

    /// <summary>
    /// Interface for two-factor authentication
    /// </summary>
    public interface ITwoFactorService
    {
        Task<string> GenerateTwoFactorCodeAsync(string username);
        Task<bool> ValidateTwoFactorCodeAsync(string username, string code);
    }

    /// <summary>
    /// Interface for audit logging
    /// </summary>
    public interface IAuditLogger
    {
        Task LogLoginAttemptAsync(string username, bool success, string? reason = null);
        Task LogFailedAttemptAsync(string username, string reason);
    }
}
