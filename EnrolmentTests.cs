using System.Net;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

public class EnrolmentTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    private async Task<(HttpClient org, EventDto ev, CategoryDto cat)> Setup(int max = 100)
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var ev = await org.CreateEvent();
        var cat = await org.CreateCategory(ev.EventId, max);
        return (org, ev, cat);
    }

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecorded()
    {
        var (_, ev, cat) = await Setup();
        var part = await factory.LoginAs(UserRole.Participant);
        var me = await (await part.GetAsync("/api/users/me")).Read<UserDto>();

        var resp = await part.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var enrolment = await resp.Read<EnrolmentDto>();
        Assert.Equal(me!.UserId, enrolment!.ParticipantId);
        Assert.Equal(cat.CategoryId, enrolment.CategoryId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.False(string.IsNullOrEmpty(enrolment.BibNumber));

        var mine = await (await part.GetAsync("/api/enrolments/mine")).Read<List<EnrolmentDto>>();
        Assert.Contains(mine!, e => e.EnrolmentId == enrolment.EnrolmentId && e.Status == EnrolmentStatus.Confirmed);
    }

    [Fact]
    public async Task Enrol_Twice_ReturnsConflict()
    {
        var (_, _, cat) = await Setup();
        var part = await factory.LoginAs(UserRole.Participant);
        await part.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);
        var again = await part.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenCategoryFull_ReturnsBadRequest()
    {
        var (_, _, cat) = await Setup(max: 1);
        var first = await factory.LoginAs(UserRole.Participant);
        var second = await factory.LoginAs(UserRole.Participant);
        Assert.Equal(HttpStatusCode.Created, (await first.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await second.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null)).StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_ReturnsNotFound()
    {
        var part = await factory.LoginAs(UserRole.Participant);
        Assert.Equal(HttpStatusCode.NotFound, (await part.PostAsync("/api/categories/999999/enrol", null)).StatusCode);
    }

    [Fact]
    public async Task Enrol_Unauthenticated_ReturnsUnauthorized()
    {
        var (_, _, cat) = await Setup();
        var resp = await factory.CreateClient().PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Enrol_AsOrganiser_ReturnsForbidden()
    {
        var (org, _, cat) = await Setup();
        var resp = await org.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForOwnEvent()
    {
        var (org, ev, cat) = await Setup();
        var part = await factory.LoginAs(UserRole.Participant);
        await part.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);

        var resp = await org.GetAsync($"/api/events/{ev.EventId}/enrolments");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Single((await resp.Read<List<EnrolmentDto>>())!);
    }

    [Fact]
    public async Task ViewEventEnrolments_AsParticipant_ReturnsForbidden()
    {
        var (_, ev, _) = await Setup();
        var part = await factory.LoginAs(UserRole.Participant);
        Assert.Equal(HttpStatusCode.Forbidden, (await part.GetAsync($"/api/events/{ev.EventId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task ViewEventEnrolments_AsDifferentOrganiser_ReturnsForbidden()
    {
        var (_, ev, _) = await Setup();
        var other = await factory.LoginAs(UserRole.Organiser);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/events/{ev.EventId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task MyEnrolments_AsOrganiser_ReturnsForbidden()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.GetAsync("/api/enrolments/mine")).StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_ButNotSomeoneElses()
    {
        var (_, _, cat) = await Setup();
        var owner = await factory.LoginAs(UserRole.Participant);
        var stranger = await factory.LoginAs(UserRole.Participant);
        var enrolment = await (await owner.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null)).Read<EnrolmentDto>();

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.DeleteAsync($"/api/enrolments/{enrolment!.EnrolmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}")).StatusCode);

        var mine = await (await owner.GetAsync("/api/enrolments/mine")).Read<List<EnrolmentDto>>();
        Assert.Equal(EnrolmentStatus.Cancelled, mine!.Single().Status);
    }
}
