using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs.Events;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests;

/// <summary>Covers: different roles are rejected when attempting role-based functionalities.</summary>
public class RoleAccessTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RoleAccessTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateEvent_AsParticipant_ReturnsForbidden()
    {
        var (client, _) = await _factory.CreateParticipantClientAsync();

        var response = await client.PostAsJsonAsync("/api/events", new CreateEventDto
        {
            Name = "Participant Trying To Create An Event",
            EventDate = new DateOnly(2026, 12, 1),
            Type = EventType.Run
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutLoggingIn_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/events", new CreateEventDto
        {
            Name = "Anonymous Trying To Create An Event",
            EventDate = new DateOnly(2026, 12, 1),
            Type = EventType.Run
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EnrolInCategory_AsOrganiser_ReturnsForbidden()
    {
        var (organiserClient, _) = await _factory.CreateOrganiserClientAsync();
        var eventResponse = await organiserClient.PostAsJsonAsync("/api/events", new CreateEventDto
        {
            Name = "Gauteng Trail Challenge",
            EventDate = new DateOnly(2026, 9, 27),
            Type = EventType.Run
        });
        var ev = await eventResponse.Content.ReadFromJsonAsync<EventDto>();

        var categoryResponse = await organiserClient.PostAsJsonAsync($"/api/events/{ev!.EventId}/categories", new
        {
            name = "10km Trail",
            distanceKm = 10m,
            entryFee = 150m
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var categoryId = category.GetProperty("categoryId").GetInt32();

        // The organiser (not a Participant) tries to enrol in their own category.
        var response = await organiserClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_AsOrganiser_ReturnsForbidden()
    {
        var (client, _) = await _factory.CreateOrganiserClientAsync();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_AsParticipant_ReturnsForbidden()
    {
        var (client, _) = await _factory.CreateParticipantClientAsync();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_AsParticipant_ReturnsForbidden()
    {
        var (participantClient, _) = await _factory.CreateParticipantClientAsync();

        // Enrolment id doesn't need to exist for this check — role authorization
        // (the [Authorize(Roles = "Organiser")] attribute) runs before the handler
        // even looks the enrolment up, so a Participant is rejected regardless.
        var response = await participantClient.PostAsJsonAsync("/api/enrolments/1/results", new
        {
            finishTime = "01:45:32",
            position = 1,
            status = "Finished"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
