using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs.Events;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests;

/// <summary>Covers: Event management — verifying that Organisers can manage events.</summary>
public class EventManagementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EventManagementTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateEventDto SampleEvent(string name = "Pretoria Spring Marathon") => new()
    {
        Name = name,
        EventDate = new DateOnly(2026, 10, 18),
        Location = "Union Buildings, Pretoria",
        Description = "Annual road race through the city centre.",
        Distance = 42.2m,
        Type = EventType.Run
    };

    [Fact]
    public async Task CreateEvent_AsOrganiser_ReturnsCreatedWithPlannedStatus()
    {
        var (client, organiser) = await _factory.CreateOrganiserClientAsync();

        var response = await client.PostAsJsonAsync("/api/events", SampleEvent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.NotNull(created);
        Assert.Equal(organiser.UserId, created!.OrganiserId);
        Assert.Equal(EventStatus.Planned, created.Status);
    }

    [Fact]
    public async Task UpdateEvent_AsOwningOrganiser_UpdatesSuccessfully()
    {
        var (client, _) = await _factory.CreateOrganiserClientAsync();
        var createResponse = await client.PostAsJsonAsync("/api/events", SampleEvent());
        var created = await createResponse.Content.ReadFromJsonAsync<EventDto>();

        var update = new UpdateEventDto
        {
            Name = "Pretoria Spring Marathon (Updated)",
            EventDate = created!.EventDate,
            Location = created.Location,
            Description = "Updated description.",
            Distance = created.Distance,
            Type = EventType.Run,
            Status = EventStatus.Open
        };

        var response = await client.PutAsJsonAsync($"/api/events/{created.EventId}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal("Pretoria Spring Marathon (Updated)", updated!.Name);
        Assert.Equal(EventStatus.Open, updated.Status);
    }

    [Fact]
    public async Task UpdateEvent_AsDifferentOrganiser_ReturnsForbidden()
    {
        var (ownerClient, _) = await _factory.CreateOrganiserClientAsync();
        var createResponse = await ownerClient.PostAsJsonAsync("/api/events", SampleEvent());
        var created = await createResponse.Content.ReadFromJsonAsync<EventDto>();

        var (otherClient, _) = await _factory.CreateOrganiserClientAsync();
        var update = new UpdateEventDto
        {
            Name = "Hijacked Event",
            EventDate = created!.EventDate,
            Type = EventType.Run,
            Status = EventStatus.Cancelled
        };

        var response = await otherClient.PutAsJsonAsync($"/api/events/{created.EventId}", update);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsOwningOrganiser_RemovesEvent()
    {
        var (client, _) = await _factory.CreateOrganiserClientAsync();
        var createResponse = await client.PostAsJsonAsync("/api/events", SampleEvent("Event To Delete"));
        var created = await createResponse.Content.ReadFromJsonAsync<EventDto>();

        var deleteResponse = await client.DeleteAsync($"/api/events/{created!.EventId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/events/{created.EventId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetEvents_WithoutLoggingIn_IsPubliclyReadable()
    {
        var (ownerClient, _) = await _factory.CreateOrganiserClientAsync();
        await ownerClient.PostAsJsonAsync("/api/events", SampleEvent("Public Visibility Test"));

        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidType_ReturnsBadRequest()
    {
        var (client, _) = await _factory.CreateOrganiserClientAsync();
        var invalid = SampleEvent();
        invalid.Type = "Swim"; // not one of Run/Walk/Cycle

        var response = await client.PostAsJsonAsync("/api/events", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
