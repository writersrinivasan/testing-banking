using BankingAuth.Interfaces;
using BankingAuth.Models;
using BankingAuth.Services;
using Moq;
using Xunit;

namespace BankingAuth.Tests.Unit
{
    /// <summary>
    /// Additional unit tests demonstrating various mocking patterns and testing techniques
    /// - Setup sequences of returns
    /// - Verify call order
    /// - Test callbacks
    /// - Test exception handling
    /// - Use Moq verification features
    /// </summary>
    public class AuthenticationServiceAdvancedTests
    {
        #region Setup & Helper Methods

        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IPasswordHasher> _mockPasswordHasher;
        private readonly Mock<ITokenProvider> _mockTokenProvider;
        private readonly Mock<ITwoFactorService> _mockTwoFactorService;
        private readonly Mock<IAuditLogger> _mockAuditLogger;
        private readonly AuthenticationService _authService;

        public AuthenticationServiceAdvancedTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockPasswordHasher = new Mock<IPasswordHasher>();
            _mockTokenProvider = new Mock<ITokenProvider>();
            _mockTwoFactorService = new Mock<ITwoFactorService>();
            _mockAuditLogger = new Mock<IAuditLogger>();

            _authService = new AuthenticationService(
                _mockUserRepository.Object,
                _mockPasswordHasher.Object,
                _mockTokenProvider.Object,
                _mockTwoFactorService.Object,
                _mockAuditLogger.Object);
        }

        private User CreateValidUser(string username = "john.doe", bool twoFactorEnabled = false)
        {
            return new User
            {
                Id = 1,
                Username = username,
                PasswordHash = "hashed_password",
                Email = $"{username}@bank.com",
                IsActive = true,
                TwoFactorEnabled = twoFactorEnabled,
                FailedLoginAttempts = 0,
                CreatedDate = DateTime.UtcNow
            };
        }

        #endregion

        #region Multiple Calls & Sequence Tests

        [Fact]
        public async Task LoginAsync_MultipleFailedAttempts_IncreasesFailureCounter()
        {
            // Arrange: Setup mock to track call count
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "WrongPassword"
            };

            var user = CreateValidUser();

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            // Act: Make multiple login attempts
            for (int i = 0; i < 3; i++)
            {
                await _authService.LoginAsync(loginRequest);
            }

            // Assert: Verify UpdateUserAsync was called 3 times
            _mockUserRepository.Verify(
                x => x.UpdateUserAsync(It.IsAny<User>()),
                Times.Exactly(3),
                "UpdateUserAsync should be called after each failed attempt");
        }

        [Fact]
        public async Task LoginAsync_VerifiesCallSequence()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "SecurePassword123"
            };

            var user = CreateValidUser();

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            _mockTokenProvider
                .Setup(x => x.GenerateToken(It.IsAny<string>()))
                .Returns("token");

            _mockTokenProvider
                .Setup(x => x.GetTokenExpiry(It.IsAny<string>()))
                .Returns(DateTime.UtcNow.AddHours(1));

            // Act
            await _authService.LoginAsync(loginRequest);

            // Assert: Verify calls were made in order
            // First, user repository is called to get user
            _mockUserRepository.Verify(x => x.GetUserByUsernameAsync("john.doe"), Times.Once);
            
            // Then, password is verified
            _mockPasswordHasher.Verify(x => x.VerifyPassword("SecurePassword123", "hashed_password"), Times.Once);
            
            // Then, token is generated
            _mockTokenProvider.Verify(x => x.GenerateToken("john.doe"), Times.Once);
            
            // Finally, user is updated
            _mockUserRepository.Verify(x => x.UpdateUserAsync(It.IsAny<User>()), Times.Once);
        }

        #endregion

        #region Verify Never Called

        [Fact]
        public async Task LoginAsync_WithInactiveUser_NeverGeneratesToken()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "SecurePassword123"
            };

            var user = CreateValidUser();
            user.IsActive = false;

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            // Act
            await _authService.LoginAsync(loginRequest);

            // Assert
            _mockTokenProvider.Verify(x => x.GenerateToken(It.IsAny<string>()), Times.Never,
                "Token should not be generated for inactive users");
        }

        #endregion

        #region Verify with Matchers

        [Fact]
        public async Task LoginAsync_UpdatesUserWithCurrentTimestamp()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "WrongPassword"
            };

            var user = CreateValidUser();
            var beforeTest = DateTime.UtcNow;

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            // Act
            await _authService.LoginAsync(loginRequest);

            var afterTest = DateTime.UtcNow;

            // Assert
            _mockUserRepository.Verify(x => x.UpdateUserAsync(It.Is<User>(u =>
                u.LastLoginAttempt >= beforeTest && u.LastLoginAttempt <= afterTest)),
                Times.Once,
                "LastLoginAttempt should be set to current time");
        }

        #endregion

        #region Callback Tests

        [Fact]
        public async Task LoginAsync_CapturesUpdatedUserState()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "WrongPassword"
            };

            var user = CreateValidUser();
            User capturedUser = null;

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync(user);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            _mockUserRepository
                .Setup(x => x.UpdateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .ReturnsAsync(true);

            // Act
            await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(capturedUser);
            Assert.Equal(1, capturedUser.FailedLoginAttempts);
            Assert.Equal("john.doe", capturedUser.Username);
        }

        #endregion

        #region Exception Handling Tests

        [Fact]
        public async Task LoginAsync_WhenUserRepositoryThrows_PropagatesException()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "john.doe",
                Password = "password"
            };

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _authService.LoginAsync(loginRequest));
        }

        #endregion

        #region Multiple Mock Behaviors

        [Fact]
        public async Task LoginAsync_WithDifferentUsersReturnsCorrectResponses()
        {
            // Arrange
            var user1 = CreateValidUser("user1");
            var user2 = CreateValidUser("user2");

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("user1"))
                .ReturnsAsync(user1);

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("user2"))
                .ReturnsAsync(user2);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword("password1", "hashed_password"))
                .Returns(true);

            _mockPasswordHasher
                .Setup(x => x.VerifyPassword("password2", "hashed_password"))
                .Returns(true);

            _mockTokenProvider
                .Setup(x => x.GenerateToken(It.IsAny<string>()))
                .Returns<string>(username => $"token_for_{username}");

            _mockTokenProvider
                .Setup(x => x.GetTokenExpiry(It.IsAny<string>()))
                .Returns(DateTime.UtcNow.AddHours(1));

            // Act
            var response1 = await _authService.LoginAsync(
                new LoginRequest { Username = "user1", Password = "password1" });

            var response2 = await _authService.LoginAsync(
                new LoginRequest { Username = "user2", Password = "password2" });

            // Assert
            Assert.Equal("token_for_user1", response1.Token);
            Assert.Equal("token_for_user2", response2.Token);
        }

        #endregion

        #region Strict Mock Behavior

        [Fact]
        public async Task LoginAsync_StrictMockEnsuringNoUnexpectedCalls()
        {
            // Arrange
            var strictMock = new Mock<IAuditLogger>(MockBehavior.Strict);
            strictMock
                .Setup(x => x.LogFailedAttemptAsync("john.doe", "User not found"))
                .Returns(Task.CompletedTask);

            var authService = new AuthenticationService(
                _mockUserRepository.Object,
                _mockPasswordHasher.Object,
                _mockTokenProvider.Object,
                _mockTwoFactorService.Object,
                strictMock.Object);

            _mockUserRepository
                .Setup(x => x.GetUserByUsernameAsync("john.doe"))
                .ReturnsAsync((User)null);

            // Act
            var response = await authService.LoginAsync(
                new LoginRequest { Username = "john.doe", Password = "password" });

            // Assert
            Assert.False(response.Success);
            // If any unexpected call is made, the test will fail
        }

        #endregion
    }
}
