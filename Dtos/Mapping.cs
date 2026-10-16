using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public static class Mapping
{
    public static UserDto ToDto(this User u) => new(u.UserId, u.FullName, u.Email, u.Role, u.PhoneNumber, u.CreatedAt);

    public static VenueDto ToDto(this Venue v) => new(v.VenueId, v.AddressLine, v.City, v.Province, v.Latitude, v.Longitude);

    public static CategoryDto ToDto(this Category c) => new(c.CategoryId, c.EventId, c.Name, c.DistanceKm, c.MaxParticipants, c.EntryFee);

    public static EventDto ToDto(this Event e) => new(e.EventId, e.OrganiserId, e.Name, e.Description, e.EventDate,
        e.EventType, e.Status, e.Venue?.ToDto(), e.Categories.Select(c => c.ToDto()).ToList());

    /// <summary>Requires Participant and Category.Event to be loaded.</summary>
    public static EnrolmentDto ToDto(this Enrolment e) => new(e.EnrolmentId, e.ParticipantId, e.Participant.FullName,
        e.CategoryId, e.Category.Name, e.Category.EventId, e.Category.Event.Name, e.Category.Event.EventDate,
        e.BibNumber, e.EnrolmentDate, e.Status);

    /// <summary>Requires Enrolment.Participant and Enrolment.Category.Event to be loaded.</summary>
    public static ResultDto ToDto(this Result r) => new(r.ResultId, r.EnrolmentId, r.Enrolment.Participant.FullName,
        r.Enrolment.BibNumber, r.Enrolment.Category.EventId, r.Enrolment.Category.Event.Name, r.Enrolment.Category.Name,
        r.FinishTime, r.OverallPosition, r.CategoryPosition, r.RaceStatus, r.RecordedAt);
}
