using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public class EnrolmentsController(RaceDayDbContext db) : ControllerBase
{
    private IQueryable<Enrolment> Full => db.Enrolments.Include(e => e.Participant).Include(e => e.Category).ThenInclude(c => c.Event);

    /// <summary>Enrol the logged-in Participant into a category (Participant only).</summary>
    /// <remarks>A bib number is generated automatically. Re-enrolling after cancelling re-activates the old enrolment.</remarks>
    /// <response code="201">The enrolment, including bib number.</response>
    /// <response code="400">Category is full or the event is not open for enrolment.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller is not a Participant.</response>
    /// <response code="404">Category not found.</response>
    /// <response code="409">Already enrolled in this category.</response>
    [HttpPost("api/categories/{categoryId:int}/enrol")]
    [Authorize(Roles = "Participant")]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int categoryId)
    {
        var uid = User.UserId();
        var cat = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (cat is null) return NotFound();
        if (cat.Event.Status != EventStatus.Published)
            return Problem("This event is not open for enrolment.", statusCode: 400);

        var existing = await db.Enrolments.FirstOrDefaultAsync(e => e.ParticipantId == uid && e.CategoryId == categoryId);
        if (existing is not null && existing.Status != EnrolmentStatus.Cancelled)
            return Problem("You are already enrolled in this category.", statusCode: 409);

        var active = await db.Enrolments.CountAsync(e => e.CategoryId == categoryId && e.Status != EnrolmentStatus.Cancelled);
        if (active >= cat.MaxParticipants)
            return Problem("This category is full.", statusCode: 400);

        Enrolment enrolment;
        if (existing is not null)
        {
            existing.Status = EnrolmentStatus.Confirmed;
            existing.EnrolmentDate = DateTime.UtcNow;
            enrolment = existing;
        }
        else
        {
            var total = await db.Enrolments.CountAsync(e => e.CategoryId == categoryId);
            enrolment = new Enrolment
            {
                ParticipantId = uid,
                CategoryId = categoryId,
                BibNumber = $"{categoryId}-{total + 1:D3}",
                Status = EnrolmentStatus.Confirmed
            };
            db.Enrolments.Add(enrolment);
        }
        await db.SaveChangesAsync();

        var dto = (await Full.AsNoTracking().FirstAsync(e => e.EnrolmentId == enrolment.EnrolmentId)).ToDto();
        return Created("/api/enrolments/mine", dto);
    }

    /// <summary>List all enrolments of the logged-in Participant (Participant only).</summary>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("api/enrolments/mine")]
    [Authorize(Roles = "Participant")]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var uid = User.UserId();
        var list = await Full.AsNoTracking().Where(e => e.ParticipantId == uid).OrderByDescending(e => e.EnrolmentDate).ToListAsync();
        return Ok(list.Select(e => e.ToDto()).ToList());
    }

    /// <summary>Cancel your own enrolment (Participant only).</summary>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="403">Not a Participant, or not your enrolment.</response>
    /// <response code="404">Enrolment not found.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [Authorize(Roles = "Participant")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await db.Enrolments.FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();
        if (enrolment.ParticipantId != User.UserId()) return Forbid();
        enrolment.Status = EnrolmentStatus.Cancelled;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>List all enrolments for an event you own (Organiser only).</summary>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="403">Not an Organiser, or not the event's owner.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int eventId)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.UserId()) return Forbid();
        var list = await Full.AsNoTracking().Where(e => e.Category.EventId == eventId).OrderBy(e => e.CategoryId).ThenBy(e => e.BibNumber).ToListAsync();
        return Ok(list.Select(e => e.ToDto()).ToList());
    }
}
