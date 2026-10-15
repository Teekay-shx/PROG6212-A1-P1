using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Api.DTOs.Events;
using RaceDay.Api.DTOs.Results;
using RaceDay.Api.DTOs.Users;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests;

/// <summary>Covers: User Profile (view/update own profile) and Results (capture, correct, and view).</summary>
public class UserProfileAndResultsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UserProfileAndResultsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateMyProfile_ChangesPersistAndAreReturnedByGetMe()
    {
        var (client, _) = await _factory.CreateParticipantClientAsync();

        var update = new UpdateProfileDto
        {
            FullName = "Updated Name",
            Phone = "0712223333",
            DateOfBirth = new DateOnly(1995, 5, 5)
        };
        var putResponse = await client.PutAsJsonAsync("/api/users/me", update);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var getResponse = await client.GetFromJsonAsync<UserProfileDto>("/api/users/me");
        Assert.Equal("Updated Name", getResponse!.FullName);
        Assert.Equal("0712223333", getResponse.Phone);
    }

    private async Task<(HttpClient OrganiserClient, HttpClient ParticipantClient, int EnrolmentId, int CategoryId)> SetUpEnrolledParticipantAsync()
    {
        var (organiserClient, _) = await _factory.CreateOrganiserClientAsync();
        var eventResponse = await organiserClient.PostAsJsonAsync("/api/events", new CreateEventDto
        {
            Name = "Gauteng Trail Challenge",
            EventDate = new DateOnly(2026, 9, 27),
            Type = EventType.Run,
            Distance = 21m
        });
        var ev = await eventResponse.Content.ReadFromJsonAsync<EventDto>();

        var categoryResponse = await organiserClient.PostAsJsonAsync($"/api/events/{ev!.EventId}/categories", new
        {
            name = "21km Trail",
            distanceKm = 21m,
            entryFee = 220m
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var categoryId = category.GetProperty("categoryId").GetInt32();

        var (participantClient, _) = await _factory.CreateParticipantClientAsync();
        var enrolResponse = await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);
        var enrolment = await enrolResponse.Content.ReadFromJsonAsync<JsonElement>();
        var enrolmentId = enrolment.GetProperty("enrolmentId").GetInt32();

        return (organiserClient, participantClient, enrolmentId, categoryId);
    }

    [Fact]
    public async Task RecordResult_AsOwningOrganiser_IsVisibleOnPublicLeaderboard()
    {
        var (organiserClient, _, enrolmentId, categoryId) = await SetUpEnrolledParticipantAsync();

        var response = await organiserClient.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new CreateResultDto
        {
            FinishTime = new TimeOnly(1, 45, 32),
            Position = 1,
            Status = ResultStatus.Finished
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var leaderboard = await _factory.CreateClient()
            .GetFromJsonAsync<List<ResultDto>>($"/api/categories/{categoryId}/results");
        Assert.Contains(leaderboard!, r => r.Position == 1);
    }

    [Fact]
    public async Task RecordResult_Twice_ForSameEnrolment_ReturnsConflict()
    {
        var (organiserClient, _, enrolmentId, _) = await SetUpEnrolledParticipantAsync();

        var first = await organiserClient.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new CreateResultDto
        {
            Position = 1,
            Status = ResultStatus.Finished
        });
        var second = await organiserClient.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new CreateResultDto
        {
            Position = 2,
            Status = ResultStatus.Finished
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetMyResults_AsParticipant_ReturnsTheirOwnResultOnly()
    {
        var (organiserClient, participantClient, enrolmentId, _) = await SetUpEnrolledParticipantAsync();
        await organiserClient.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new CreateResultDto
        {
            Position = 3,
            Status = ResultStatus.Finished
        });

        var response = await participantClient.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<ResultDto>>();
        Assert.Single(results!);
        Assert.Equal(3, results![0].Position);
    }
}
