# Functional & UI Testing Guide for Banking Flows

This guide provides recommendations for extending the unit tests to functional and UI testing, with specific examples for banking authentication workflows.

## Testing Pyramid

```
         /\
        /  \  E2E & UI Tests
       /    \  (Small number, slow)
      /______\
     /        \
    /  Integ   \ Integration Tests
   /   Tests    \ (Medium number)
  /____________\
 /              \
/ Unit Tests     \ Unit Tests
\  (Many, fast)  / (90% of tests)
 \______________/
```

## Recommended Tools & Frameworks

### UI Automation
- **Selenium WebDriver** - Web automation (most popular)
- **Playwright** - Modern browser automation
- **Cypress** - Fast, reliable E2E testing
- **Appium** - Mobile app testing

### BDD Frameworks
- **SpecFlow** - Gherkin syntax with C#
- **Cucumber** - BDD with multiple languages

### Load & Performance
- **k6** - Modern load testing
- **NBomber** - .NET load testing
- **JMeter** - Apache load testing

### API Testing
- **RestSharp** - REST client for C#
- **HttpClient** - Built-in .NET HTTP client
- **Postman** - Manual and automated API testing

## Functional Test Examples

### Example 1: Login Workflow with Selenium

```csharp
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;

namespace BankingAuth.Tests.Functional
{
    [Collection("Browser")]
    public class LoginUITests : IDisposable
    {
        private readonly IWebDriver _driver;
        private const string BaseUrl = "https://localhost:5001";

        public LoginUITests()
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            _driver = new ChromeDriver(options);
        }

        [Fact]
        public void LoginFlow_WithValidCredentials_DisplaysDashboard()
        {
            // Arrange
            _driver.Navigate().GoToUrl($"{BaseUrl}/login");
            var usernameField = _driver.FindElement(By.Id("username"));
            var passwordField = _driver.FindElement(By.Id("password"));
            var loginButton = _driver.FindElement(By.Id("login-btn"));

            // Act
            usernameField.SendKeys("john.doe");
            passwordField.SendKeys("SecurePassword123");
            loginButton.Click();

            // Assert - Wait for dashboard to load
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            var dashboard = wait.Until(d => d.FindElement(By.Id("dashboard-container")));
            
            Assert.NotNull(dashboard);
            Assert.Contains("Welcome", _driver.PageSource);
        }

        [Fact]
        public void LoginFlow_WithInvalidPassword_ShowsErrorMessage()
        {
            // Arrange
            _driver.Navigate().GoToUrl($"{BaseUrl}/login");
            var usernameField = _driver.FindElement(By.Id("username"));
            var passwordField = _driver.FindElement(By.Id("password"));
            var loginButton = _driver.FindElement(By.Id("login-btn"));

            // Act
            usernameField.SendKeys("john.doe");
            passwordField.SendKeys("WrongPassword");
            loginButton.Click();

            // Assert
            var errorMessage = _driver.FindElement(By.Id("error-message"));
            Assert.NotNull(errorMessage);
            Assert.Contains("Invalid credentials", errorMessage.Text);
        }

        [Fact]
        public void LoginFlow_AfterFailedAttempts_ShowsAccountLocked()
        {
            // Act - Try logging in 5 times with wrong password
            for (int i = 0; i < 5; i++)
            {
                _driver.Navigate().GoToUrl($"{BaseUrl}/login");
                var usernameField = _driver.FindElement(By.Id("username"));
                var passwordField = _driver.FindElement(By.Id("password"));
                var loginButton = _driver.FindElement(By.Id("login-btn"));

                usernameField.SendKeys("john.doe");
                passwordField.SendKeys("WrongPassword");
                loginButton.Click();

                System.Threading.Thread.Sleep(500); // Wait for response
            }

            // Assert - On 6th attempt, should show lockout message
            _driver.Navigate().GoToUrl($"{BaseUrl}/login");
            var usernameField6 = _driver.FindElement(By.Id("username"));
            var passwordField6 = _driver.FindElement(By.Id("password"));
            var loginButton6 = _driver.FindElement(By.Id("login-btn"));

            usernameField6.SendKeys("john.doe");
            passwordField6.SendKeys("WrongPassword");
            loginButton6.Click();

            var lockoutMessage = _driver.FindElement(By.Id("error-message"));
            Assert.NotNull(lockoutMessage);
            Assert.Contains("temporarily locked", lockoutMessage.Text);
        }

        [Fact]
        public void TwoFactorAuthFlow_WithValidCode_LogsInSuccessfully()
        {
            // Arrange
            _driver.Navigate().GoToUrl($"{BaseUrl}/login");
            var usernameField = _driver.FindElement(By.Id("username"));
            var passwordField = _driver.FindElement(By.Id("password"));
            var loginButton = _driver.FindElement(By.Id("login-btn"));

            // Act - First step: enter credentials
            usernameField.SendKeys("john.2fa@bank.com");
            passwordField.SendKeys("SecurePassword123");
            loginButton.Click();

            // Assert - 2FA screen appears
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            var twoFactorForm = wait.Until(d => d.FindElement(By.Id("2fa-form")));
            Assert.NotNull(twoFactorForm);

            // Act - Enter 2FA code (in real scenario, this comes from authenticator app)
            var twoFactorField = _driver.FindElement(By.Id("2fa-code"));
            twoFactorField.SendKeys("123456");
            var submitButton = _driver.FindElement(By.Id("submit-2fa"));
            submitButton.Click();

            // Assert - Logged in successfully
            var dashboard = wait.Until(d => d.FindElement(By.Id("dashboard-container")));
            Assert.NotNull(dashboard);
        }

        public void Dispose()
        {
            _driver?.Dispose();
        }
    }
}
```

### Example 2: BDD Test with SpecFlow

```gherkin
Feature: Banking Authentication
    As a bank customer
    I want to securely log into my account
    So that I can manage my finances

    Scenario: Successful login with valid credentials
        Given I am on the login page
        When I enter username "john.doe"
        And I enter password "SecurePassword123"
        And I click the login button
        Then I should be on the dashboard
        And I should see "Welcome, John Doe"

    Scenario: Login fails with invalid password
        Given I am on the login page
        When I enter username "john.doe"
        And I enter password "WrongPassword"
        And I click the login button
        Then I should see an error message
        And the error message should contain "Invalid credentials"

    Scenario: Account locks after 5 failed attempts
        Given I am on the login page
        When I attempt to login 5 times with wrong password for "john.doe"
        And I attempt to login again with wrong password
        Then I should see "Account is temporarily locked"

    Scenario: Two-factor authentication required
        Given I am on the login page
        When I enter username "john.2fa@bank.com"
        And I enter password "SecurePassword123"
        And I click the login button
        Then I should see the 2FA code entry field
        When I enter the 2FA code "123456"
        And I click submit
        Then I should be on the dashboard

    Scenario: Session expires after inactivity
        Given I am logged in
        And I have been inactive for 30 minutes
        When I attempt to access a protected resource
        Then I should be redirected to the login page
        And I should see "Your session has expired"
```

### Example 3: SpecFlow Step Definitions

```csharp
using TechTalk.SpecFlow;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace BankingAuth.Tests.Functional.Steps
{
    [Binding]
    public class AuthenticationSteps
    {
        private readonly IWebDriver _driver;
        private const string BaseUrl = "https://localhost:5001";

        public AuthenticationSteps(BrowserFixture browser)
        {
            _driver = browser.Driver;
        }

        [Given(@"I am on the login page")]
        public void GivenImOnTheLoginPage()
        {
            _driver.Navigate().GoToUrl($"{BaseUrl}/login");
        }

        [When(@"I enter username ""(.+)""")]
        public void WhenIEnterUsername(string username)
        {
            var field = _driver.FindElement(By.Id("username"));
            field.SendKeys(username);
        }

        [When(@"I enter password ""(.+)""")]
        public void WhenIEnterPassword(string password)
        {
            var field = _driver.FindElement(By.Id("password"));
            field.SendKeys(password);
        }

        [When(@"I click the login button")]
        public void WhenIClickTheLoginButton()
        {
            var button = _driver.FindElement(By.Id("login-btn"));
            button.Click();
        }

        [Then(@"I should be on the dashboard")]
        public void ThenIShouldBeOnTheDashboard()
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            var dashboard = wait.Until(d => d.FindElement(By.Id("dashboard-container")));
            Assert.NotNull(dashboard);
        }

        [Then(@"I should see ""(.+)""")]
        public void ThenIShouldSee(string text)
        {
            Assert.Contains(text, _driver.PageSource);
        }

        [Then(@"I should see an error message")]
        public void ThenIShouldSeeAnErrorMessage()
        {
            var errorElement = _driver.FindElement(By.Id("error-message"));
            Assert.NotNull(errorElement);
            Assert.True(errorElement.Displayed);
        }

        [Then(@"the error message should contain ""(.+)""")]
        public void ThenTheErrorMessageShouldContain(string text)
        {
            var errorElement = _driver.FindElement(By.Id("error-message"));
            Assert.Contains(text, errorElement.Text);
        }
    }
}
```

## Performance Testing Example

```csharp
using NBomber.CSharp;
using NBomber.Http.CSharp;
using Xunit;

namespace BankingAuth.Tests.Performance
{
    public class AuthenticationPerformanceTests
    {
        [Fact]
        public void LoginEndpoint_UnderLoad_MeetsPerformanceTarget()
        {
            var httpClient = new HttpClient();

            var scenario = Scenario.Create("login_scenario", async context =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost:5001/api/auth/login");
                var content = new StringContent(
                    @"{""username"":""john.doe"",""password"":""SecurePassword123""}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                request.Content = content;

                var response = await httpClient.SendAsync(request);
                
                return response.IsSuccessStatusCode 
                    ? Response.Ok() 
                    : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
            );

            var assertions = new[]
            {
                Assertion.ForScenario("login_scenario", stats =>
                {
                    // Average response time should be < 500ms
                    Assert.True(stats.RPS.Mean < 500, "Average response time exceeded 500ms");
                    
                    // P95 response time should be < 1000ms
                    Assert.True(stats.RPS.Percentile(95.0) < 1000, "95th percentile response time exceeded 1000ms");
                    
                    // All requests should succeed
                    Assert.True(stats.FailCount == 0, $"Had {stats.FailCount} failed requests");
                })
            };

            var nBomberRunner = NBomberRunner
                .RegisterScenarios(scenario)
                .WithAssertions(assertions)
                .Run();
        }
    }
}
```

## API Testing Example

```csharp
using RestSharp;
using Xunit;

namespace BankingAuth.Tests.API
{
    public class AuthenticationAPITests
    {
        private const string BaseUrl = "https://localhost:5001";
        private readonly RestClient _client;

        public AuthenticationAPITests()
        {
            _client = new RestClient(BaseUrl);
        }

        [Fact]
        public async Task LoginAPI_WithValidCredentials_ReturnsToken()
        {
            // Arrange
            var request = new RestRequest("/api/auth/login", Method.Post);
            request.AddJsonBody(new
            {
                username = "john.doe",
                password = "SecurePassword123"
            });

            // Act
            var response = await _client.ExecuteAsync(request);

            // Assert
            Assert.True(response.IsSuccessful);
            Assert.NotNull(response.Content);
            Assert.Contains("token", response.Content.ToLower());
        }

        [Fact]
        public async Task ValidateTokenAPI_WithValidToken_ReturnsUserInfo()
        {
            // Arrange - First get a token
            var loginRequest = new RestRequest("/api/auth/login", Method.Post);
            loginRequest.AddJsonBody(new
            {
                username = "john.doe",
                password = "SecurePassword123"
            });
            var loginResponse = await _client.ExecuteAsync(loginRequest);
            var token = ExtractTokenFromResponse(loginResponse.Content);

            // Act - Validate the token
            var validateRequest = new RestRequest("/api/auth/validate", Method.Post);
            validateRequest.AddHeader("Authorization", $"Bearer {token}");
            var response = await _client.ExecuteAsync(validateRequest);

            // Assert
            Assert.True(response.IsSuccessful);
            Assert.Contains("john.doe", response.Content);
        }

        [Fact]
        public async Task LoginAPI_WithInvalidPassword_ReturnsBadRequest()
        {
            // Arrange
            var request = new RestRequest("/api/auth/login", Method.Post);
            request.AddJsonBody(new
            {
                username = "john.doe",
                password = "WrongPassword"
            });

            // Act
            var response = await _client.ExecuteAsync(request);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("Invalid credentials", response.Content);
        }

        private string ExtractTokenFromResponse(string jsonResponse)
        {
            // Simple token extraction (use JSON parsing in production)
            var startIndex = jsonResponse.IndexOf("\"token\":\"") + 9;
            var endIndex = jsonResponse.IndexOf("\"", startIndex);
            return jsonResponse.Substring(startIndex, endIndex - startIndex);
        }
    }
}
```

## Test Automation Best Practices

### 1. Test Environment Setup
```csharp
[CollectionDefinition("Browser collection")]
public class BrowserCollection : ICollectionFixture<BrowserFixture>
{
}

public class BrowserFixture : IAsyncLifetime
{
    public IWebDriver Driver { get; private set; }

    public async Task InitializeAsync()
    {
        var options = new ChromeOptions();
        options.AddArgument("--no-sandbox");
        options.AddArgument("--headless");
        Driver = new ChromeDriver(options);
        
        // Wait for app to be ready
        await Task.Delay(2000);
    }

    public async Task DisposeAsync()
    {
        Driver?.Dispose();
        await Task.CompletedTask;
    }
}
```

### 2. Page Object Model Pattern
```csharp
public class LoginPage
{
    private readonly IWebDriver _driver;

    public LoginPage(IWebDriver driver)
    {
        _driver = driver;
    }

    public IWebElement UsernameField => _driver.FindElement(By.Id("username"));
    public IWebElement PasswordField => _driver.FindElement(By.Id("password"));
    public IWebElement LoginButton => _driver.FindElement(By.Id("login-btn"));
    public IWebElement ErrorMessage => _driver.FindElement(By.Id("error-message"));

    public void EnterCredentials(string username, string password)
    {
        UsernameField.SendKeys(username);
        PasswordField.SendKeys(password);
    }

    public void ClickLogin()
    {
        LoginButton.Click();
    }

    public string GetErrorMessage()
    {
        return ErrorMessage.Text;
    }
}

// Usage in tests
[Fact]
public void LoginFlow_WithInvalidCredentials_ShowsError()
{
    var loginPage = new LoginPage(_driver);
    loginPage.EnterCredentials("john", "wrong");
    loginPage.ClickLogin();
    
    Assert.Contains("Invalid", loginPage.GetErrorMessage());
}
```

### 3. Parallel Test Execution
```xml
<!-- xunit.runner.json -->
{
  "parallelizeAssembly": true,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 4
}
```

### 4. Test Data Management
```csharp
public class TestDataBuilder
{
    private string _username = "test.user";
    private string _password = "SecurePassword123";
    private bool _twoFactorEnabled = false;

    public TestDataBuilder WithUsername(string username)
    {
        _username = username;
        return this;
    }

    public TestDataBuilder WithTwoFactor()
    {
        _twoFactorEnabled = true;
        return this;
    }

    public LoginRequest Build()
    {
        return new LoginRequest
        {
            Username = _username,
            Password = _password,
            TwoFactorCode = _twoFactorEnabled ? "123456" : null
        };
    }
}

// Usage
var request = new TestDataBuilder()
    .WithUsername("john.doe")
    .WithTwoFactor()
    .Build();
```

## Continuous Integration Setup

### GitHub Actions Example
```yaml
name: Test Automation

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v2
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: '9.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Run Unit Tests
      run: dotnet test --filter "TestClass=*Unit*" --no-build --verbosity normal
    
    - name: Run Functional Tests
      run: dotnet test --filter "TestClass=*Functional*" --no-build --verbosity normal
    
    - name: Upload Coverage
      uses: codecov/codecov-action@v2
      with:
        files: ./coverage.xml
```

## Summary

This guide provides a complete testing strategy for banking authentication flows:

- **Unit Tests**: 34 comprehensive tests for business logic
- **Integration Tests**: Model validation and API integration
- **Functional Tests**: Selenium WebDriver for UI testing
- **BDD Tests**: SpecFlow for behavior-driven development
- **Performance Tests**: NBomber for load testing
- **API Tests**: RestSharp for API endpoint testing

All tests follow professional best practices and can be integrated into CI/CD pipelines for continuous quality assurance.
