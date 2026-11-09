using System.Net;
using System.Net.Http.Headers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>Part 3 additions: image upload rules (checked before anything reaches Blob Storage) and venue editing.</summary>
public class UploadAndVenueTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    private static MultipartFormDataContent FilePart(string contentType, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, "file", "upload.bin");
        return form;
    }

    [Fact]
    public async Task UploadBanner_Unauthenticated_ReturnsUnauthorized()
    {
        var resp = await factory.CreateClient().PostAsync("/api/events/1/banner", FilePart("image/png", new byte[] { 1, 2, 3 }));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_AsParticipant_ReturnsForbidden()
    {
        var part = await factory.LoginAs(UserRole.Participant);
        var resp = await part.PostAsync("/api/events/1/banner", FilePart("image/png", new byte[] { 1, 2, 3 }));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_NonImage_ReturnsBadRequest()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var ev = await org.CreateEvent("Banner Test");
        var resp = await org.PostAsync($"/api/events/{ev.EventId}/banner", FilePart("text/plain", new byte[] { 1, 2, 3 }));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_OtherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await factory.LoginAs(UserRole.Organiser);
        var ev = await owner.CreateEvent("Not Yours");
        var other = await factory.LoginAs(UserRole.Organiser);
        var resp = await other.PostAsync($"/api/events/{ev.EventId}/banner", FilePart("image/png", new byte[] { 1, 2, 3 }));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePicture_NonImage_ReturnsBadRequest()
    {
        var part = await factory.LoginAs(UserRole.Participant);
        var resp = await part.PostAsync("/api/users/me/picture", FilePart("application/pdf", new byte[] { 1, 2, 3 }));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_WithVenue_ChangesVenue()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var ev = await org.CreateEvent("Venue Move");
        var resp = await org.PutJson($"/api/events/{ev.EventId}", new UpdateEventRequest
        {
            Name = ev.Name,
            Description = ev.Description,
            EventDate = ev.EventDate,
            EventType = ev.EventType,
            Venue = new VenueRequest { AddressLine = "9 New St", City = "Durban", Province = "KwaZulu-Natal" }
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Read<EventDto>();
        Assert.Equal("Durban", updated!.Venue!.City);
        Assert.Equal("KwaZulu-Natal", updated.Venue.Province);
    }

    [Fact]
    public async Task UpdateEvent_WithoutVenue_KeepsVenue()
    {
        var org = await factory.LoginAs(UserRole.Organiser);
        var ev = await org.CreateEvent("Venue Stay");
        var resp = await org.PutJson($"/api/events/{ev.EventId}", new UpdateEventRequest
        {
            Name = "Renamed", EventDate = ev.EventDate, EventType = ev.EventType
        });
        var updated = await resp.Read<EventDto>();
        Assert.Equal("Pretoria", updated!.Venue!.City);
    }
}
