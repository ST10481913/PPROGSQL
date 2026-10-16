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
public class CategoriesController(RaceDayDbContext db) : ControllerBase
{
    /// <summary>List the categories of an event (public).</summary>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(int eventId)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == eventId)) return NotFound();
        var cats = await db.Categories.AsNoTracking().Where(c => c.EventId == eventId).OrderBy(c => c.DistanceKm).ToListAsync();
        return Ok(cats.Select(c => c.ToDto()).ToList());
    }

    /// <summary>Add a category (e.g. 10km, Under 20, Senior) to an event you own (Organiser only).</summary>
    /// <response code="201">The created category.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="403">Not an Organiser, or not the event's owner.</response>
    /// <response code="404">Event not found.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, [FromBody] CategoryRequest req)
    {
        var ev = await db.Events.FindAsync(eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.UserId()) return Forbid();

        var cat = new Category
        {
            EventId = eventId,
            Name = req.Name.Trim(),
            DistanceKm = req.DistanceKm,
            MaxParticipants = req.MaxParticipants,
            EntryFee = req.EntryFee
        };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return Created($"/api/events/{eventId}/categories", cat.ToDto());
    }

    /// <summary>Update a category of an event you own (Organiser only).</summary>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="403">Not an Organiser, or not the event's owner.</response>
    /// <response code="404">Category not found.</response>
    [HttpPut("api/categories/{id:int}")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest req)
    {
        var cat = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat is null) return NotFound();
        if (cat.Event.OrganiserId != User.UserId()) return Forbid();

        cat.Name = req.Name.Trim();
        cat.DistanceKm = req.DistanceKm;
        cat.MaxParticipants = req.MaxParticipants;
        cat.EntryFee = req.EntryFee;
        await db.SaveChangesAsync();
        return Ok(cat.ToDto());
    }

    /// <summary>Delete a category of an event you own, only if nobody has enrolled (Organiser only).</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Not an Organiser, or not the event's owner.</response>
    /// <response code="404">Category not found.</response>
    /// <response code="409">Category has enrolments.</response>
    [HttpDelete("api/categories/{id:int}")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat is null) return NotFound();
        if (cat.Event.OrganiserId != User.UserId()) return Forbid();
        if (await db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return Problem("This category has enrolments and cannot be deleted.", statusCode: 409);
        db.Categories.Remove(cat);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
