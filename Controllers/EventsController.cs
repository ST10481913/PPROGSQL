using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController(RaceDayDbContext db, WeatherService weather) : ControllerBase
{
    private IQueryable<Event> Full => db.Events.Include(e => e.Venue).Include(e => e.Categories);

    /// <summary>List all published events (public). Optionally filter by type, date or province.</summary>
    /// <param name="type">Run, Walk or Cycle.</param>
    /// <param name="date">Only events happening on this date (yyyy-MM-dd).</param>
    /// <param name="province">Only events whose venue is in this province.</param>
    /// <response code="200">Array of events.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] EventType? type, [FromQuery] DateTime? date, [FromQuery] string? province)
    {
        var q = Full.AsNoTracking().Where(e => e.Status == EventStatus.Published);
        if (type is not null) q = q.Where(e => e.EventType == type);
        if (date is not null) q = q.Where(e => e.EventDate.Date == date.Value.Date);
        if (!string.IsNullOrWhiteSpace(province))
            q = q.Where(e => e.Venue != null && e.Venue.Province == province);
        var events = await q.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToDto()).ToList());
    }

    /// <summary>List all events created by the logged-in Organiser (Organiser only).</summary>
    /// <response code="200">Array of the organiser's events (all statuses).</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpGet("mine")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var uid = User.UserId();
        var events = await Full.AsNoTracking().Where(e => e.OrganiserId == uid).OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToDto()).ToList());
    }

    /// <summary>Get one event with its venue and categories (public).</summary>
    /// <response code="200">The event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await Full.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        var isOwner = User.Identity?.IsAuthenticated == true && User.UserId() == ev.OrganiserId;
        if (ev.Status == EventStatus.Draft && !isOwner) return NotFound();
        return Ok(ev.ToDto());
    }

    /// <summary>Create an event with its venue (Organiser only).</summary>
    /// <response code="201">The created event.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpPost]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest req)
    {
        var ev = new Event
        {
            OrganiserId = User.UserId(),
            Name = req.Name.Trim(),
            Description = req.Description,
            EventDate = req.EventDate!.Value,
            EventType = req.EventType!.Value,
            Status = EventStatus.Published,
            Venue = new Venue
            {
                AddressLine = req.Venue.AddressLine,
                City = req.Venue.City,
                Province = req.Venue.Province,
                Latitude = req.Venue.Latitude,
                Longitude = req.Venue.Longitude
            }
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, ev.ToDto());
    }

    /// <summary>Update an event you own (Organiser only).</summary>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="403">Not an Organiser, or not the event's owner.</response>
    /// <response code="404">Event not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEventRequest req)
    {
        var ev = await Full.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.UserId()) return Forbid();

        ev.Name = req.Name.Trim();
        ev.Description = req.Description;
        ev.EventDate = req.EventDate!.Value;
        ev.EventType = req.EventType!.Value;
        if (req.Status is not null) ev.Status = req.Status.Value;
        await db.SaveChangesAsync();
        return Ok(ev.ToDto());
    }

    /// <summary>Delete an event you own (Organiser only). Fails if participants have enrolled.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Not an Organiser, or not the event's owner.</response>
    /// <response code="404">Event not found.</response>
    /// <response code="409">Event has enrolments; cancel it instead.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.UserId()) return Forbid();
        if (await db.Enrolments.AnyAsync(en => en.Category.EventId == id))
            return Problem("This event has enrolments and cannot be deleted. Set its status to Cancelled instead.", statusCode: 409);
        db.Events.Remove(ev);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Get the weather forecast for the event's venue and date (public, via Open-Meteo).</summary>
    /// <remarks>Forecasts are only available roughly 16 days ahead and need venue coordinates.</remarks>
    /// <response code="200">Forecast data.</response>
    /// <response code="404">Event not found, or no forecast available.</response>
    [HttpGet("{id:int}/weather")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Weather(int id)
    {
        var ev = await db.Events.AsNoTracking().Include(e => e.Venue).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.Venue?.Latitude is null || ev.Venue.Longitude is null)
            return Problem("This event's venue has no coordinates, so no forecast is available.", statusCode: 404);
        var forecast = await weather.GetForecastAsync(ev.Venue.Latitude.Value, ev.Venue.Longitude.Value, ev.EventDate);
        return forecast is null
            ? Problem("No forecast available for this date.", statusCode: 404)
            : Ok(forecast);
    }
}
