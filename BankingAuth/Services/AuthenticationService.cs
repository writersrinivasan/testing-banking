using BankingAuth.Interfaces;
using BankingAuth.Models;

namespace BankingAuth.Services
{
    public class AuthenticationService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenProvider _tokenProvider;
        private readonly ITwoFactorService _twoFactorService;
        private readonly IAuditLogger _auditLogger;
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        public AuthenticationService(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            ITokenProvider tokenProvider,
            ITwoFactorService twoFactorService,
            IAuditLogger auditLogger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            _twoFactorService = twoFactorService ?? throw new ArgumentNullException(nameof(twoFactorService));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            // Validation
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                await _auditLogger.LogFailedAttemptAsync(request?.Username ?? "UNKNOWN", "Invalid username or password");
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Username and password are required"
                };
            }

            // Get user from repository
            var user = await _userRepository.GetUserByUsernameAsync(request.Username);

            if (user == null)
            {
                await _auditLogger.LogFailedAttemptAsync(request.Username, "User not found");
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid credentials"
                };
            }

            // Check if user is active
            if (!user.IsActive)
            {
                await _auditLogger.LogFailedAttemptAsync(request.Username, "User account is inactive");
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "User account is inactive"
                };
            }

            // Check for account lockout
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                var timeSinceLastAttempt = DateTime.UtcNow - (user.LastLoginAttempt ?? DateTime.UtcNow);
                if (timeSinceLastAttempt.TotalMinutes < LockoutMinutes)
                {
                    await _auditLogger.LogFailedAttemptAsync(request.Username, "Account locked due to failed attempts");
                    return new LoginResponse
                    {
                        Success = false,
                        ErrorMessage = "Account is temporarily locked. Please try again later."
                    };
                }
            }

            // Verify password
            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;
                user.LastLoginAttempt = DateTime.UtcNow;
                await _userRepository.UpdateUserAsync(user);
                await _auditLogger.LogFailedAttemptAsync(request.Username, "Invalid password");

                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid credentials"
                };
            }

            // Check for two-factor authentication
            if (user.TwoFactorEnabled)
            {
                await _auditLogger.LogLoginAttemptAsync(request.Username, false, "Awaiting 2FA code");
                return new LoginResponse
                {
                    Success = false,
                    RequiresTwoFactor = true,
                    ErrorMessage = "Two-factor authentication required"
                };
            }

            // Reset failed attempts on successful login
            user.FailedLoginAttempts = 0;
            user.LastLoginAttempt = DateTime.UtcNow;
            await _userRepository.UpdateUserAsync(user);

            // Generate token
            var token = _tokenProvider.GenerateToken(user.Username);
            var tokenExpiry = _tokenProvider.GetTokenExpiry(token);

            await _auditLogger.LogLoginAttemptAsync(request.Username, true);

            return new LoginResponse
            {
                Success = true,
                Token = token,
                TokenExpiry = tokenExpiry
            };
        }

        public async Task<LoginResponse> ValidateTwoFactorAsync(LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Username and 2FA code are required"
                };
            }

            // Validate 2FA code
            var isValid = await _twoFactorService.ValidateTwoFactorCodeAsync(request.Username, request.TwoFactorCode);

            if (!isValid)
            {
                await _auditLogger.LogFailedAttemptAsync(request.Username, "Invalid 2FA code");
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid two-factor code"
                };
            }

            // Get user and generate token
            var user = await _userRepository.GetUserByUsernameAsync(request.Username);
            if (user == null)
            {
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "User not found"
                };
            }

            var token = _tokenProvider.GenerateToken(user.Username);
            var tokenExpiry = _tokenProvider.GetTokenExpiry(token);

            await _auditLogger.LogLoginAttemptAsync(request.Username, true);

            return new LoginResponse
            {
                Success = true,
                Token = token,
                TokenExpiry = tokenExpiry
            };
        }
    }
}
