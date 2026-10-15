using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs.Auth;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests;

/// <summary>Covers: User registration and login (success and failure scenarios).</summary>
public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithValidParticipantDetails_ReturnsCreatedAndUser()
    {
        var client = _factory.CreateClient();
        var dto = new RegisterDto
        {
            FullName = "Thabo Nkosi",
            Email = $"{Guid.NewGuid():N}@example.com",
            Password = "Password123!",
            Role = UserRole.Participant
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(dto.Email, body!.Email);
        Assert.Equal(UserRole.Participant, body.Role);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var dto = new RegisterDto
        {
            FullName = "Lerato Dlamini",
            Email = $"{Guid.NewGuid():N}@example.com",
            Password = "Password123!",
            Role = UserRole.Participant
        };

        var first = await client.PostAsJsonAsync("/api/auth/register", dto);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Same email, different client so the second call isn't short-circuited by
        // already being signed in from the first registration.
        var secondClient = _factory.CreateClient();
        var second = await secondClient.PostAsJsonAsync("/api/auth/register", dto);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidRole_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var dto = new RegisterDto
        {
            FullName = "Shady Admin",
            Email = $"{Guid.NewGuid():N}@example.com",
            Password = "Password123!",
            Role = UserRole.Admin // self-registering as Admin must be rejected
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsOkAndSetsSessionCookie()
    {
        var registerClient = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var password = "Password123!";

        await registerClient.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            FullName = "Naledi Khumalo",
            Email = email,
            Password = password,
            Role = UserRole.Organiser
        });

        // Fresh client (no cookie yet) exercises login independently of registration.
        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // The session cookie should now let this same client reach an authenticated endpoint.
        var meResponse = await loginClient.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var registerClient = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        await registerClient.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            FullName = "Johan van der Merwe",
            Email = email,
            Password = "Password123!",
            Role = UserRole.Organiser
        });

        var loginClient = _factory.CreateClient();
        var response = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "nobody-registered@example.com",
            Password = "Whatever123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutLoggingIn_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
