using System.Net;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

public class EventTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var resp = await org.PostJson("/api/events", TestApi.NewEvent("Create Test"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var ev = await resp.Read<EventDto>();
        Assert.Equal("Create Test", ev!.Name);
        Assert.Equal("Pretoria", ev.Venue!.City);
    }

    [Fact]
    public async Task CreateEvent_InvalidBody_ReturnsBadRequest()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var bad = TestApi.NewEvent();
        bad.Name = "";
        var resp = await org.PostJson("/api/events", bad);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_Unauthenticated_ReturnsUnauthorized()
    {
        var resp = await factory.CreateClient().PostJson("/api/events", TestApi.NewEvent());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsParticipant_ReturnsForbidden()
    {
        var part = await factory.LoginAs(UserRole.Participant);
        var resp = await part.PostJson("/api/events", TestApi.NewEvent());
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Events_CanBeViewedPubliclyAndFiltered()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var ev = await org.CreateEvent("Public Visible Event");

        var anon = factory.CreateClient();
        var list = await (await anon.GetAsync("/api/events")).Read<List<EventDto>>();
        Assert.Contains(list!, e => e.EventId == ev.EventId);

        var filtered = await (await anon.GetAsync("/api/events?type=Cycle")).Read<List<EventDto>>();
        Assert.DoesNotContain(filtered!, e => e.EventId == ev.EventId);

        var single = await anon.GetAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }

    [Fact]
    public async Task GetEvent_NotFound_Returns404()
    {
        var resp = await factory.CreateClient().GetAsync("/api/events/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var ev = await org.CreateEvent();
        var resp = await org.PutJson($"/api/events/{ev.EventId}", new UpdateEventRequest
        {
            Name = "Renamed Event", Description = "x", EventDate = DateTime.UtcNow.AddDays(40),
            EventType = EventType.Walk, Status = EventStatus.Published
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Read<EventDto>();
        Assert.Equal("Renamed Event", updated!.Name);
        Assert.Equal(EventType.Walk, updated.EventType);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent()
    {
        var owner = await factory.LoginAs(UserRole.Organiser);
        var other = await factory.LoginAs(UserRole.Organiser);
        var ev = await owner.CreateEvent();
        var resp = await other.PutJson($"/api/events/{ev.EventId}", new UpdateEventRequest
        {
            Name = "Hijack", EventDate = DateTime.UtcNow.AddDays(5), EventType = EventType.Run
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var ev = await org.CreateEvent();
        Assert.Equal(HttpStatusCode.NoContent, (await org.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await org.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_NotFound_Returns404()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        Assert.Equal(HttpStatusCode.NotFound, (await org.DeleteAsync("/api/events/999999")).StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_ReturnsConflict()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var part = await factory.LoginAs(UserRole.Participant);
        var ev = await org.CreateEvent();
        var cat = await org.CreateCategory(ev.EventId);
        await part.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);
        Assert.Equal(HttpStatusCode.Conflict, (await org.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsParticipant_ReturnsForbidden()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var part = await factory.LoginAs(UserRole.Participant);
        var ev = await org.CreateEvent();
        Assert.Equal(HttpStatusCode.Forbidden, (await part.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task MyEvents_OnlyForOrganisers_AndOnlyOwnEvents()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var other = await factory.LoginAs(UserRole.Organiser);
        var mine = await org.CreateEvent("Mine");
        await other.CreateEvent("Not mine");

        var list = await (await org.GetAsync("/api/events/mine")).Read<List<EventDto>>();
        Assert.Contains(list!, e => e.EventId == mine.EventId);
        Assert.All(list!, e => Assert.Equal(mine.OrganiserId, e.OrganiserId));

        var part = await factory.LoginAs(UserRole.Participant);
        Assert.Equal(HttpStatusCode.Forbidden, (await part.GetAsync("/api/events/mine")).StatusCode);
    }

    [Fact]
    public async Task Categories_OrganiserCanManage_ParticipantCanOnlyView()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var part = await factory.LoginAs(UserRole.Participant);
        var ev = await org.CreateEvent();
        var cat = await org.CreateCategory(ev.EventId);

        var denied = await part.PostJson($"/api/events/{ev.EventId}/categories",
            new CategoryRequest { Name = "5km", DistanceKm = 5, MaxParticipants = 10, EntryFee = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var list = await (await part.GetAsync($"/api/events/{ev.EventId}/categories")).Read<List<CategoryDto>>();
        Assert.Contains(list!, c => c.CategoryId == cat.CategoryId);

        var upd = await org.PutJson($"/api/categories/{cat.CategoryId}",
            new CategoryRequest { Name = "21km", DistanceKm = 21.1m, MaxParticipants = 50, EntryFee = 250 });
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await part.DeleteAsync($"/api/categories/{cat.CategoryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await org.DeleteAsync($"/api/categories/{cat.CategoryId}")).StatusCode);
    }
}
