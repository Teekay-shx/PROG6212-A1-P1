using System.Net.Http.Json;
using RaceDay.Api.DTOs.Auth;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

/// <summary>Shared helpers for spinning up authenticated test users and HttpClients.</summary>
public static class TestHelpers
{
    /// <summary>
    /// Registers a brand-new user (random email) with the given role and returns an
    /// HttpClient that is already signed in as that user via the session cookie set
    /// during registration.
    /// </summary>
    public static async Task<(HttpClient Client, AuthResponseDto User)> CreateAuthenticatedClientAsync(
        this CustomWebApplicationFactory factory, string role, string namePrefix = "Test User")
    {
        var client = factory.CreateClient();

        var register = new RegisterDto
        {
            FullName = $"{namePrefix} {Guid.NewGuid():N}".Substring(0, 24),
            Email = $"{Guid.NewGuid():N}@example.com",
            Password = "Password123!",
            Role = role
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", register);
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return (client, user!);
    }

    public static Task<(HttpClient Client, AuthResponseDto User)> CreateOrganiserClientAsync(this CustomWebApplicationFactory factory) =>
        factory.CreateAuthenticatedClientAsync(UserRole.Organiser, "Organiser");

    public static Task<(HttpClient Client, AuthResponseDto User)> CreateParticipantClientAsync(this CustomWebApplicationFactory factory) =>
        factory.CreateAuthenticatedClientAsync(UserRole.Participant, "Participant");
}
