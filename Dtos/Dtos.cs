using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------- Requests ----------
public class RegisterRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), StringLength(100)] public string Password { get; set; } = string.Empty;
    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required] public UserRole? Role { get; set; }
    [StringLength(20)] public string? PhoneNumber { get; set; }
    /// <summary>Invite code, required only when registering as an Organiser.</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class RefreshRequest { [Required] public string RefreshToken { get; set; } = string.Empty; }

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [StringLength(20)] public string? PhoneNumber { get; set; }
}

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required, MinLength(8), StringLength(100)] public string NewPassword { get; set; } = string.Empty;
}

public class VenueRequest
{
    [Required, StringLength(200)] public string AddressLine { get; set; } = string.Empty;
    [Required, StringLength(100)] public string City { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Province { get; set; } = string.Empty;
    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }
}

public class CreateEventRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required] public DateTime? EventDate { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public EventType? EventType { get; set; }
    [Required] public VenueRequest Venue { get; set; } = null!;
}

public class UpdateEventRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required] public DateTime? EventDate { get; set; }
    [Required] public EventType? EventType { get; set; }
    /// <summary>Optional. Draft, Published, Cancelled or Completed. Unchanged if omitted.</summary>
    public EventStatus? Status { get; set; }
}

public class CategoryRequest
{
    [Required, StringLength(50)] public string Name { get; set; } = string.Empty;
    [Range(0.01, 999.99)] public decimal DistanceKm { get; set; }
    [Range(1, 1000000)] public int MaxParticipants { get; set; }
    [Range(0, 99999.99)] public decimal EntryFee { get; set; }
}

public class ResultRequest
{
    /// <summary>Format hh:mm:ss. Required when RaceStatus is Finished.</summary>
    public TimeSpan? FinishTime { get; set; }
    [Range(1, int.MaxValue)] public int? OverallPosition { get; set; }
    [Range(1, int.MaxValue)] public int? CategoryPosition { get; set; }
    public RaceStatus RaceStatus { get; set; } = RaceStatus.Finished;
}

// ---------- Responses ----------
public record RegisterResponse(int UserId, string Email);
public record AuthResponse(string AccessToken, string RefreshToken, UserRole Role);
public record AccessTokenResponse(string AccessToken);
public record UserDto(int UserId, string FullName, string Email, UserRole Role, string? PhoneNumber, DateTime CreatedAt);
public record VenueDto(int VenueId, string AddressLine, string City, string Province, decimal? Latitude, decimal? Longitude);
public record CategoryDto(int CategoryId, int EventId, string Name, decimal DistanceKm, int MaxParticipants, decimal EntryFee);
public record EventDto(int EventId, int OrganiserId, string Name, string? Description, DateTime EventDate,
    EventType EventType, EventStatus Status, VenueDto? Venue, List<CategoryDto> Categories);
public record EnrolmentDto(int EnrolmentId, int ParticipantId, string ParticipantName, int CategoryId, string CategoryName,
    int EventId, string EventName, DateTime EventDate, string? BibNumber, DateTime EnrolmentDate, EnrolmentStatus Status);
public record ResultDto(int ResultId, int EnrolmentId, string ParticipantName, string? BibNumber, int EventId, string EventName,
    string CategoryName, TimeSpan? FinishTime, int? OverallPosition, int? CategoryPosition, RaceStatus RaceStatus, DateTime RecordedAt);
