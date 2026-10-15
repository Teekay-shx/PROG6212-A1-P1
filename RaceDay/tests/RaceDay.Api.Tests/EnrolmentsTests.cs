using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Api.DTOs.Enrolments;
using RaceDay.Api.DTOs.Events;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests;

/// <summary>Covers: Event enrolments — verifying that Participants can enrol and that the enrolment is correctly recorded.</summary>
public class EnrolmentsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EnrolmentsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient OrganiserClient, int EventId, int CategoryId)> CreateEventWithCategoryAsync()
    {
        var (organiserClient, _) = await _factory.CreateOrganiserClientAsync();

        var eventResponse = await organiserClient.PostAsJsonAsync("/api/events", new CreateEventDto
        {
            Name = "Fun Run for Charity",
            EventDate = new DateOnly(2026, 11, 8),
            Location = "Pretoria Botanical Garden",
            Type = EventType.Run,
            Distance = 5m
        });
        var ev = await eventResponse.Content.ReadFromJsonAsync<EventDto>();

        var categoryResponse = await organiserClient.PostAsJsonAsync($"/api/events/{ev!.EventId}/categories", new
        {
            name = "5km Fun Run",
            distanceKm = 5m,
            entryFee = 100m,
            maxParticipants = 2
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var categoryId = category.GetProperty("categoryId").GetInt32();

        return (organiserClient, ev.EventId, categoryId);
    }

    [Fact]
    public async Task Enrol_AsParticipant_CreatesEnrolmentWithBibNumber()
    {
        var (_, _, categoryId) = await CreateEventWithCategoryAsync();
        var (participantClient, participant) = await _factory.CreateParticipantClientAsync();

        var response = await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentDto>();
        Assert.NotNull(enrolment);
        Assert.Equal(participant.UserId, enrolment!.ParticipantId);
        Assert.Equal(categoryId, enrolment.CategoryId);
        Assert.False(string.IsNullOrWhiteSpace(enrolment.BibNumber));
        Assert.Equal(EnrolmentStatus.Registered, enrolment.Status);
    }

    [Fact]
    public async Task Enrol_ThenGetMyEnrolments_IncludesTheNewEnrolment()
    {
        var (_, _, categoryId) = await CreateEventWithCategoryAsync();
        var (participantClient, _) = await _factory.CreateParticipantClientAsync();

        await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        var response = await participantClient.GetAsync("/api/users/me/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var enrolments = await response.Content.ReadFromJsonAsync<List<EnrolmentDto>>();
        Assert.NotNull(enrolments);
        Assert.Contains(enrolments!, e => e.CategoryId == categoryId);
    }

    [Fact]
    public async Task Enrol_Twice_InSameCategory_ReturnsConflict()
    {
        var (_, _, categoryId) = await CreateEventWithCategoryAsync();
        var (participantClient, _) = await _factory.CreateParticipantClientAsync();

        var first = await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);
        var second = await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_ReturnsConflict()
    {
        // The fixture category has MaxParticipants = 2.
        var (_, _, categoryId) = await CreateEventWithCategoryAsync();

        var (first, _) = await _factory.CreateParticipantClientAsync();
        var (second, _) = await _factory.CreateParticipantClientAsync();
        var (third, _) = await _factory.CreateParticipantClientAsync();

        await first.PostAsync($"/api/categories/{categoryId}/enrolments", null);
        await second.PostAsync($"/api/categories/{categoryId}/enrolments", null);
        var thirdResponse = await third.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Conflict, thirdResponse.StatusCode);
    }

    [Fact]
    public async Task Enrol_InNonExistentCategory_ReturnsNotFound()
    {
        var (participantClient, _) = await _factory.CreateParticipantClientAsync();

        var response = await participantClient.PostAsync("/api/categories/999999/enrolments", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEnrolmentsForCategory_AsOwningOrganiser_ReturnsRoster()
    {
        var (organiserClient, _, categoryId) = await CreateEventWithCategoryAsync();
        var (participantClient, participant) = await _factory.CreateParticipantClientAsync();
        await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        var response = await organiserClient.GetAsync($"/api/categories/{categoryId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roster = await response.Content.ReadFromJsonAsync<List<EnrolmentDto>>();
        Assert.Contains(roster!, e => e.ParticipantId == participant.UserId);
    }

    [Fact]
    public async Task CancelEnrolment_AsOwningParticipant_RemovesIt()
    {
        var (_, _, categoryId) = await CreateEventWithCategoryAsync();
        var (participantClient, _) = await _factory.CreateParticipantClientAsync();
        var created = await (await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null))
            .Content.ReadFromJsonAsync<EnrolmentDto>();

        var response = await participantClient.DeleteAsync($"/api/enrolments/{created!.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var myEnrolments = await participantClient.GetFromJsonAsync<List<EnrolmentDto>>("/api/users/me/enrolments");
        Assert.DoesNotContain(myEnrolments!, e => e.EnrolmentId == created.EnrolmentId);
    }

    [Fact]
    public async Task CancelEnrolment_AsDifferentParticipant_ReturnsForbidden()
    {
        var (_, _, categoryId) = await CreateEventWithCategoryAsync();
        var (ownerClient, _) = await _factory.CreateParticipantClientAsync();
        var created = await (await ownerClient.PostAsync($"/api/categories/{categoryId}/enrolments", null))
            .Content.ReadFromJsonAsync<EnrolmentDto>();

        var (otherClient, _) = await _factory.CreateParticipantClientAsync();
        var response = await otherClient.DeleteAsync($"/api/enrolments/{created!.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
