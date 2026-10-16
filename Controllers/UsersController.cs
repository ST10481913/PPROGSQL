using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users/me")]
[Authorize]
[Produces("application/json")]
public class UsersController(RaceDayDbContext db) : ControllerBase
{
    /// <summary>Get the logged-in user's profile (any role).</summary>
    /// <response code="200">The user's profile.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        var user = await db.Users.FindAsync(User.UserId());
        return user is null ? NotFound() : Ok(user.ToDto());
    }

    /// <summary>Update the logged-in user's full name and phone number (any role).</summary>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPut]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(User.UserId());
        if (user is null) return NotFound();
        user.FullName = req.FullName.Trim();
        user.PhoneNumber = req.PhoneNumber;
        await db.SaveChangesAsync();
        return Ok(user.ToDto());
    }

    /// <summary>Change the logged-in user's password (any role).</summary>
    /// <response code="200">Password changed.</response>
    /// <response code="400">Current password is wrong or new password is invalid.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var user = await db.Users.FindAsync(User.UserId());
        if (user is null) return NotFound();
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return Problem("Current password is incorrect.", statusCode: 400);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();
        return Ok();
    }
}
