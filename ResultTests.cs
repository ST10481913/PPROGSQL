using System.Net;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

public class ResultTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    private static ResultRequest Finished(int pos = 1) => new()
    {
        FinishTime = TimeSpan.FromMinutes(45), OverallPosition = pos, CategoryPosition = pos, RaceStatus = RaceStatus.Finished
    };

    private async Task<(HttpClient org, HttpClient part, EnrolmentDto enrolment)> Setup()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var part = await factory.LoginAs(UserRole.Participant);
        var ev = await org.CreateEvent();
        var cat = await org.CreateCategory(ev.EventId);
        var enrolment = await (await part.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null)).Read<EnrolmentDto>();
        return (org, part, enrolment!);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanViewIt()
    {
        var (org, part, enrolment) = await Setup();
        var resp = await org.PostJson($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished());
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var mine = await (await part.GetAsync("/api/results/mine")).Read<List<ResultDto>>();
        var result = Assert.Single(mine!);
        Assert.Equal(1, result.OverallPosition);
        Assert.Equal(TimeSpan.FromMinutes(45), result.FinishTime);
    }

    [Fact]
    public async Task CaptureResult_AsParticipant_ReturnsForbidden()
    {
        var (_, part, enrolment) = await Setup();
        Assert.Equal(HttpStatusCode.Forbidden, (await part.PostJson($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished())).StatusCode);
    }

    [Fact]
    public async Task CaptureResult_AsDifferentOrganiser_ReturnsForbidden()
    {
        var (_, _, enrolment) = await Setup();
        var other = await factory.LoginAs(UserRole.Organiser);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostJson($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished())).StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_ReturnsConflict()
    {
        var (org, _, enrolment) = await Setup();
        await org.PostJson($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished());
        Assert.Equal(HttpStatusCode.Conflict, (await org.PostJson($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished())).StatusCode);
    }

    [Fact]
    public async Task CaptureResult_FinishedWithoutTime_ReturnsBadRequest()
    {
        var (org, _, enrolment) = await Setup();
        var resp = await org.PostJson($"/api/enrolments/{enrolment.EnrolmentId}/result", new ResultRequest { RaceStatus = RaceStatus.Finished });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_UnknownEnrolment_ReturnsNotFound()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        Assert.Equal(HttpStatusCode.NotFound, (await org.PostJson("/api/enrolments/999999/result", Finished())).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectResult()
    {
        var (org, _, enrolment) = await Setup();
        var created = await (await org.PostJson($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished(5))).Read<ResultDto>();
        var resp = await org.PutJson($"/api/results/{created!.ResultId}", Finished(2));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(2, (await resp.Read<ResultDto>())!.OverallPosition);
    }

    [Fact]
    public async Task Leaderboard_IsPublic()
    {
        var (org, _, enrolment) = await Setup();
        await org.PostJson($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished());
        var resp = await factory.CreateClient().GetAsync($"/api/events/{enrolment.EventId}/results");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Single((await resp.Read<List<ResultDto>>())!);
    }

    [Fact]
    public async Task MyResults_AsOrganiser_ReturnsForbidden()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.GetAsync("/api/results/mine")).StatusCode);
    }

    [Fact]
    public async Task MyResults_Unauthenticated_ReturnsUnauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/results/mine")).StatusCode);
    }
}
