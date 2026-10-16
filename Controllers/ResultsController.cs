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
public class ResultsController(RaceDayDbContext db) : ControllerBase
{
    private IQueryable<Result> Full => db.Results
        .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
        .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event);

    private static string? Validate(ResultRequest req) =>
        req.RaceStatus == RaceStatus.Finished && req.FinishTime is null
            ? "FinishTime is required when RaceStatus is Finished."
            : null;

    /// <summary>Capture the race result for an enrolment in one of your events (Organiser only).</summary>
    /// <response code="201">The captured result.</response>
    /// <response code="400">Validation error, or the enrolment was cancelled.</response>
    /// <response code="403">Not an Organiser, or not the event's owner.</response>
    /// <response code="404">Enrolment not found.</response>
    /// <response code="409">A result already exists for this enrolment.</response>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(int enrolmentId, [FromBody] ResultRequest req)
    {
        var enrolment = await db.Enrolments.Include(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment is null) return NotFound();
        if (enrolment.Category.Event.OrganiserId != User.UserId()) return Forbid();
        if (enrolment.Status == EnrolmentStatus.Cancelled)
            return Problem("Cannot capture a result for a cancelled enrolment.", statusCode: 400);
        if (await db.Results.AnyAsync(r => r.EnrolmentId == enrolmentId))
            return Problem("A result already exists for this enrolment. Use PUT /api/results/{id} to correct it.", statusCode: 409);
        if (Validate(req) is { } error) return Problem(error, statusCode: 400);

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = req.FinishTime,
            OverallPosition = req.OverallPosition,
            CategoryPosition = req.CategoryPosition,
            RaceStatus = req.RaceStatus
        };
        db.Results.Add(result);
        await db.SaveChangesAsync();

        var dto = (await Full.AsNoTracking().FirstAsync(r => r.ResultId == result.ResultId)).ToDto();
        return Created($"/api/events/{dto.EventId}/results", dto);
    }

    /// <summary>Correct a previously captured result (Organiser only).</summary>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="403">Not an Organiser, or not the event's owner.</response>
    /// <response code="404">Result not found.</response>
    [HttpPut("api/results/{id:int}")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ResultRequest req)
    {
        var result = await Full.FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFound();
        if (result.Enrolment.Category.Event.OrganiserId != User.UserId()) return Forbid();
        if (Validate(req) is { } error) return Problem(error, statusCode: 400);

        result.FinishTime = req.FinishTime;
        result.OverallPosition = req.OverallPosition;
        result.CategoryPosition = req.CategoryPosition;
        result.RaceStatus = req.RaceStatus;
        result.RecordedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(result.ToDto());
    }

    /// <summary>Public leaderboard: all results for an event, ordered by overall position.</summary>
    /// <response code="200">Array of results.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int eventId)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == eventId)) return NotFound();
        var list = await Full.AsNoTracking()
            .Where(r => r.Enrolment.Category.EventId == eventId)
            .OrderBy(r => r.OverallPosition == null).ThenBy(r => r.OverallPosition)
            .ToListAsync();
        return Ok(list.Select(r => r.ToDto()).ToList());
    }

    /// <summary>The logged-in Participant's own result history (Participant only).</summary>
    /// <response code="200">Array of the participant's results.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("api/results/mine")]
    [Authorize(Roles = "Participant")]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var uid = User.UserId();
        var list = await Full.AsNoTracking().Where(r => r.Enrolment.ParticipantId == uid)
            .OrderByDescending(r => r.RecordedAt).ToListAsync();
        return Ok(list.Select(r => r.ToDto()).ToList());
    }
}
