using BankingAuth.Interfaces;
using BankingAuth.Models;
using BankingAuth.Services;
using Moq;
using Xunit;

namespace BankingAuth.Tests.Unit
{
    /// <summary>
    /// Unit tests for AuthenticationService using xUnit and Moq
    /// Demonstrates various testing approaches including:
    /// - Mocking dependencies
    /// - Testing success scenarios
    /// - Testing error handling
    /// - Testing edge cases
    /// - AAA Pattern (Arrange, Act, Assert)
    /// </summary>
    public class AuthenticationServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IPasswordHasher> _mockPasswordHasher;
        private readonly Mock<ITokenProvider> _mockTokenProvider;
        private readonly Mock<ITwoFactorService> _mockTwoFactorService;
        private readonly Mock<IAuditLogger> _mockAuditLogger;
        private readonly AuthenticationService _authService;

        public AuthenticationServiceTests()
        {
            // Arrange: Initialize all mocks
            _mockUserRepository = new Mock<IUserRepository>();
            _mockPasswordHasher = new Mock<IPasswordHasher>();
            _mockTokenProvider = new Mock<ITokenProvider>();
            _mockTwoFactorService = new Mock<ITwoFactorService>();
            _mockAuditLogger = new Mock<IAuditLogger>();

            // Instantiate the service under test with mocked dependencies
            _authService = new AuthenticationService(
                _mockUserRepository.Object,
                _mockPasswordHasher.Object,
                _mockTokenProvider.Object,
                _mockTwoFactorService.Object,
                _mockAuditLogger.Object);
        }

        #region Happy Path Tests

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ReturnsSuccessResponse()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "SecurePassword123"
            };

            var user = new User
            {
                Id = 1,
                Username = "john.doe",
                PasswordHash = "hashed_password",
                IsActive = true,
                TwoFactorEnabled = false,
                FailedLoginAttempts = 0
            };

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword("SecurePassword123", "hashed_password"))
                .Returns(true);

            _mockTokenProvider
                .Setup(x => x.GenerateToken("john.doe"))
                .Returns("valid_token_123");

            var expiry = DateTime.UtcNow.AddHours(1);
            _mockTokenProvider
                .Setup(x => x.GetTokenExpiry("valid_token_123"))
                .Returns(expiry);

            // Act
            var response = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.Equal("valid_token_123", response.Token);
            Assert.Equal(expiry, response.TokenExpiry);
            Assert.Null(response.ErrorMessage);

            // Verify mocks were called correctly
            _mockUserRepository.Verify(x => x.GetUserByUsernameAsync("john.doe"), Times.Once);
            _mockPasswordHasher.Verify(x => x.VerifyPassword("SecurePassword123", "hashed_password"), Times.Once);
            _mockTokenProvider.Verify(x => x.GenerateToken("john.doe"), Times.Once);
            _mockAuditLogger.Verify(x => x.LogLoginAttemptAsync("john.doe", true, null), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ResetsFailedAttempts()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "SecurePassword123"
            };

            var user = new User
            {
                Id = 1,
                Username = "john.doe",
                PasswordHash = "hashed_password",
                IsActive = true,
                TwoFactorEnabled = false,
                FailedLoginAttempts = 2 // Had previous failed attempts
            };

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword("SecurePassword123", "hashed_password"))
                .Returns(true);

            _mockTokenProvider
                .Setup(x => x.GenerateToken("john.doe"))
                .Returns("valid_token_123");

            _mockTokenProvider
                .Setup(x => x.GetTokenExpiry("valid_token_123"))
                .Returns(DateTime.UtcNow.AddHours(1));

            // Act
            await _authService.LoginAsync(loginRequest);

            // Assert
            _mockUserRepository.Verify(x => x.UpdateUserAsync(It.Is<User>(u =>
                u.FailedLoginAttempts == 0 && u.Username == "john.doe")), Times.Once);
        }

        #endregion

        #region Invalid Credentials Tests

        [Fact]
        public async Task LoginAsync_WithInvalidPassword_ReturnsFailureResponse()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "WrongPassword"
            };

            var user = new User
            {
                Id = 1,
                Username = "john.doe",
                PasswordHash = "hashed_password",
                IsActive = true,
                TwoFactorEnabled = false,
                FailedLoginAttempts = 0
            };

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword("WrongPassword", "hashed_password"))
                .Returns(false);

            // Act
            var response = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Equal("Invalid credentials", response.ErrorMessage);
            Assert.Null(response.Token);

            // Verify failed attempt was incremented
            _mockUserRepository.Verify(x => x.UpdateUserAsync(It.Is<User>(u =>
                u.FailedLoginAttempts == 1)), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WithNonexistentUser_ReturnsFailureResponse()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "nonexistent.user",
                Password = "Password123"
            };

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("nonexistent.user"))
                .ReturnsAsync((User)null);

            // Act
            var response = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Equal("Invalid credentials", response.ErrorMessage);
            _mockAuditLogger.Verify(x => x.LogFailedAttemptAsync("nonexistent.user", "User not found"), Times.Once);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public async Task LoginAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authService.LoginAsync(null));
        }

        [Theory]
        [InlineData("", "password")]
        [InlineData("username", "")]
        [InlineData(null, "password")]
        [InlineData("username", null)]
        [InlineData("   ", "password")]
        public async Task LoginAsync_WithEmptyCredentials_ReturnsFailureResponse(string username, string password)
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = username,
                Password = password
            };

            // Act
            var response = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Equal("Username and password are required", response.ErrorMessage);
        }

        #endregion

        #region Account Status Tests

        [Fact]
        public async Task LoginAsync_WithInactiveUser_ReturnsFailureResponse()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "SecurePassword123"
            };

            var user = new User
            {
                Id = 1,
                Username = "john.doe",
                PasswordHash = "hashed_password",
                IsActive = false, // Inactive account
                TwoFactorEnabled = false,
                FailedLoginAttempts = 0
            };

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            // Act
            var response = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Equal("User account is inactive", response.ErrorMessage);
            _mockAuditLogger.Verify(x => x.LogFailedAttemptAsync("john.doe", "User account is inactive"), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WithLockedAccount_ReturnsLockoutResponse()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "SecurePassword123"
            };

            var user = new User
            {
                Id = 1,
                Username = "john.doe",
                PasswordHash = "hashed_password",
                IsActive = true,
                TwoFactorEnabled = false,
                FailedLoginAttempts = 5, // Max failed attempts
                LastLoginAttempt = DateTime.UtcNow.AddMinutes(-5) // 5 minutes ago (still in lockout)
            };

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            // Act
            var response = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.Contains("temporarily locked", response.ErrorMessage);
            _mockAuditLogger.Verify(x => x.LogFailedAttemptAsync("john.doe", "Account locked due to failed attempts"), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WithExpiredLockout_AllowsLogin()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "SecurePassword123"
            };

            var user = new User
            {
                Id = 1,
                Username = "john.doe",
                PasswordHash = "hashed_password",
                IsActive = true,
                TwoFactorEnabled = false,
                FailedLoginAttempts = 5,
                LastLoginAttempt = DateTime.UtcNow.AddMinutes(-20) // 20 minutes ago (lockout expired)
            };

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword("SecurePassword123", "hashed_password"))
                .Returns(true);

            _mockTokenProvider
                .Setup(x => x.GenerateToken("john.doe"))
                .Returns("valid_token");

            _mockTokenProvider
                .Setup(x => x.GetTokenExpiry("valid_token"))
                .Returns(DateTime.UtcNow.AddHours(1));

            // Act
            var response = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.True(response.Success);
            Assert.NotNull(response.Token);
        }

        #endregion

        #region Two-Factor Authentication Tests

        [Fact]
        public async Task LoginAsync_WithTwoFactorEnabled_ReturnsPendingTwoFactorResponse()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "SecurePassword123"
            };

            var user = new User
            {
                Id = 1,
                Username = "john.doe",
                PasswordHash = "hashed_password",
                IsActive = true,
                TwoFactorEnabled = true, // 2FA enabled
                FailedLoginAttempts = 0
            };

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword("SecurePassword123", "hashed_password"))
                .Returns(true);

            // Act
            var response = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.True(response.RequiresTwoFactor);
            Assert.Equal("Two-factor authentication required", response.ErrorMessage);
            _mockTokenProvider.Verify(x => x.GenerateToken(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ValidateTwoFactorAsync_WithValidCode_ReturnsSuccessResponse()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                TwoFactorCode = "123456"
            };

            var user = new User
            {
                Id = 1,
                Username = "john.doe",
                PasswordHash = "hashed_password",
                IsActive = true,
                TwoFactorEnabled = true
            };

            _mockTwoFactorService
                .Setup(x => x.ValidateTwoFactorCodeAsync("john.doe", "123456"))
                .ReturnsAsync(true);

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            _mockTokenProvider
                .Setup(x => x.GenerateToken("john.doe"))
                .Returns("valid_token");

            _mockTokenProvider
                .Setup(x => x.GetTokenExpiry("valid_token"))
                .Returns(DateTime.UtcNow.AddHours(1));

            // Act
            var response = await _authService.ValidateTwoFactorAsync(loginRequest);

            // Assert
            Assert.True(response.Success);
            Assert.Equal("valid_token", response.Token);
            _mockAuditLogger.Verify(x => x.LogLoginAttemptAsync("john.doe", true, null), Times.Once);
        }

        [Fact]
        public async Task ValidateTwoFactorAsync_WithInvalidCode_ReturnsFailureResponse()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                TwoFactorCode = "wrong_code"
            };

            _mockTwoFactorService
                .Setup(x => x.ValidateTwoFactorCodeAsync("john.doe", "wrong_code"))
                .ReturnsAsync(false);

            // Act
            var response = await _authService.ValidateTwoFactorAsync(loginRequest);

            // Assert
            Assert.False(response.Success);
            Assert.Equal("Invalid two-factor code", response.ErrorMessage);
            _mockAuditLogger.Verify(x => x.LogFailedAttemptAsync("john.doe", "Invalid 2FA code"), Times.Once);
        }

        #endregion

        #region Dependency Null Tests

        [Fact]
        public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AuthenticationService(
                null,
                _mockPasswordHasher.Object,
                _mockTokenProvider.Object,
                _mockTwoFactorService.Object,
                _mockAuditLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullPasswordHasher_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AuthenticationService(
                _mockUserRepository.Object,
                null,
                _mockTokenProvider.Object,
                _mockTwoFactorService.Object,
                _mockAuditLogger.Object));
        }

        #endregion
    }
}
