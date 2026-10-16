using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(RaceDayDbContext db, TokenService tokens, IConfiguration config) : ControllerBase
{
    /// <summary>Register a new account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Organisers must supply the organiser invite code (configured on the server) so that
    /// anyone cannot simply self-register as an Organiser. Passwords are hashed with BCrypt.
    /// </remarks>
    /// <response code="201">Account created. Returns the user id and email.</response>
    /// <response code="400">Validation error or missing/invalid organiser code.</response>
    /// <response code="409">Email already in use.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (req.Role == UserRole.Organiser)
        {
            var required = config["Registration:OrganiserInviteCode"];
            if (!string.IsNullOrEmpty(required) && req.OrganiserCode != required)
                return Problem("A valid organiser invite code is required to register as an Organiser.", statusCode: 400);
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Problem("Email already in use.", statusCode: 409);

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = req.Role!.Value,
            PhoneNumber = req.PhoneNumber
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Created("/api/users/me", new RegisterResponse(user.UserId, user.Email));
    }

    /// <summary>Log in with email and password.</summary>
    /// <response code="200">Returns a JWT access token, a refresh token and the user's role.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Problem("Invalid email or password.", statusCode: 401);

        var refresh = tokens.CreateRefreshToken(user.UserId);
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync();
        return Ok(new AuthResponse(tokens.CreateAccessToken(user), refresh.Token, user.Role));
    }

    /// <summary>Exchange a valid refresh token for a new access token.</summary>
    /// <response code="200">Returns a new access token.</response>
    /// <response code="401">Refresh token is invalid or expired.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var stored = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.Token == req.RefreshToken);
        if (stored is null || stored.ExpiresAt <= DateTime.UtcNow)
            return Problem("Refresh token is invalid or expired.", statusCode: 401);
        return Ok(new AccessTokenResponse(tokens.CreateAccessToken(stored.User)));
    }

    /// <summary>Log out by revoking the supplied refresh token.</summary>
    /// <response code="204">Token revoked.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        var uid = User.UserId();
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == req.RefreshToken && t.UserId == uid);
        if (stored is not null)
        {
            db.RefreshTokens.Remove(stored);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }
}
