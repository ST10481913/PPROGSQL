using System.Net;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

public class AuthTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Register_Participant_ReturnsCreated()
    {
        var client = factory.CreateClient();
        var resp = await client.PostJson("/api/auth/register", TestApi.NewRegistration(UserRole.Participant));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Read<RegisterResponse>();
        Assert.True(body!.UserId > 0);
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_ReturnsCreated()
    {
        var resp = await factory.CreateClient().PostJson("/api/auth/register", TestApi.NewRegistration(UserRole.Organiser));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_ReturnsBadRequest()
    {
        var reg = TestApi.NewRegistration(UserRole.Organiser);
        reg.OrganiserCode = null;
        var resp = await factory.CreateClient().PostJson("/api/auth/register", reg);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var reg = TestApi.NewRegistration(UserRole.Participant);
        await client.PostJson("/api/auth/register", reg);
        var resp = await client.PostJson("/api/auth/register", reg);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmailAndShortPassword_ReturnsBadRequest()
    {
        var reg = TestApi.NewRegistration(UserRole.Participant, "not-an-email");
        reg.Password = "short";
        var resp = await factory.CreateClient().PostJson("/api/auth/register", reg);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_MissingRole_ReturnsBadRequest()
    {
        var reg = TestApi.NewRegistration(UserRole.Participant);
        reg.Role = null;
        var resp = await factory.CreateClient().PostJson("/api/auth/register", reg);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndRole()
    {
        var client = factory.CreateClient();
        var reg = TestApi.NewRegistration(UserRole.Participant);
        await client.PostJson("/api/auth/register", reg);
        var resp = await client.PostJson("/api/auth/login", new LoginRequest { Email = reg.Email, Password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var auth = await resp.Read<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(auth!.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.Equal(UserRole.Participant, auth.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var reg = TestApi.NewRegistration(UserRole.Participant);
        await client.PostJson("/api/auth/register", reg);
        var resp = await client.PostJson("/api/auth/login", new LoginRequest { Email = reg.Email, Password = "WrongPassword1" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsUnauthorized()
    {
        var resp = await factory.CreateClient().PostJson("/api/auth/login",
            new LoginRequest { Email = "nobody@test.com", Password = "whatever123" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewAccessToken_AndLogoutRevokesIt()
    {
        var client = factory.CreateClient();
        var reg = TestApi.NewRegistration(UserRole.Participant);
        await client.PostJson("/api/auth/register", reg);
        var auth = await (await client.PostJson("/api/auth/login", new LoginRequest { Email = reg.Email, Password = TestApi.Password })).Read<AuthResponse>();

        var refreshed = await client.PostJson("/api/auth/refresh", new RefreshRequest { RefreshToken = auth!.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        var logout = await client.PostJson("/api/auth/logout", new RefreshRequest { RefreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var after = await client.PostJson("/api/auth/refresh", new RefreshRequest { RefreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ReturnsUnauthorized()
    {
        var resp = await factory.CreateClient().PostJson("/api/auth/refresh", new RefreshRequest { RefreshToken = "bogus" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Profile_Unauthenticated_ReturnsUnauthorized()
    {
        var resp = await factory.CreateClient().GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Profile_GetAndUpdate_WorksForBothRoles()
    {
        foreach (var role in new[] { UserRole.Organiser, UserRole.Participant })
        {
            var client = await factory.LoginAs(role);
            var me = await (await client.GetAsync("/api/users/me")).Read<UserDto>();
            Assert.Equal(role, me!.Role);

            var upd = await client.PutJson("/api/users/me", new UpdateProfileRequest { FullName = "Renamed", PhoneNumber = "0831112222" });
            Assert.Equal(HttpStatusCode.OK, upd.StatusCode);
            Assert.Equal("Renamed", (await upd.Read<UserDto>())!.FullName);
        }
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsBadRequest()
    {
        var client = await factory.LoginAs(UserRole.Participant);
        var resp = await client.PutJson("/api/users/me/password",
            new ChangePasswordRequest { CurrentPassword = "nope-nope-1", NewPassword = "NewPassw0rd!" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
