namespace RaceDay.Api.Models;

public enum UserRole { Organiser, Participant }
public enum EventType { Run, Walk, Cycle }
public enum EventStatus { Draft, Published, Cancelled, Completed }
public enum EnrolmentStatus { Pending, Confirmed, Cancelled }
public enum RaceStatus { Finished, DNF, DQ }

public class User
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RefreshToken
{
    public int TokenId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public User Organiser { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public EventType EventType { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Published;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Venue? Venue { get; set; }
    public List<Category> Categories { get; set; } = new();
}

public class Venue
{
    public int VenueId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int MaxParticipants { get; set; }
    public decimal EntryFee { get; set; }
}

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public User Participant { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string? BibNumber { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Confirmed;
    public Result? Result { get; set; }
}

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment Enrolment { get; set; } = null!;
    public TimeSpan? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public RaceStatus RaceStatus { get; set; } = RaceStatus.Finished;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
