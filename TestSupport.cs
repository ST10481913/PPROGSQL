using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>Boots the real API in memory, swapping SQL Server for an EF Core in-memory database.</summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    public const string OrganiserCode = "TEST-ORG-CODE";
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Registration:OrganiserInviteCode"] = OrganiserCode }));

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}

public static class TestApi
{
    public const string Password = "Passw0rd!123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<HttpResponseMessage> PostJson<T>(this HttpClient c, string url, T body) => c.PostAsJsonAsync(url, body, Json);
    public static Task<HttpResponseMessage> PutJson<T>(this HttpClient c, string url, T body) => c.PutAsJsonAsync(url, body, Json);
    public static Task<T?> Read<T>(this HttpResponseMessage r) => r.Content.ReadFromJsonAsync<T>(Json);

    public static RegisterRequest NewRegistration(UserRole role, string? email = null) => new()
    {
        FullName = "Test User",
        Email = email ?? $"{Guid.NewGuid():N}@test.com",
        Password = Password,
        Role = role,
        PhoneNumber = "0821234567",
        OrganiserCode = role == UserRole.Organiser ? RaceDayFactory.OrganiserCode : null
    };

    /// <summary>Registers a fresh user, logs in and returns a client carrying the bearer token.</summary>
    public static async Task<HttpClient> LoginAs(this RaceDayFactory f, UserRole role)
    {
        var client = f.CreateClient();
        var reg = NewRegistration(role);
        (await client.PostJson("/api/auth/register", reg)).EnsureSuccessStatusCode();
        var login = await client.PostJson("/api/auth/login", new LoginRequest { Email = reg.Email, Password = Password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Read<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    public static CreateEventRequest NewEvent(string name = "Test Run") => new()
    {
        Name = name,
        Description = "A test event",
        EventDate = DateTime.UtcNow.AddDays(30),
        EventType = EventType.Run,
        Venue = new VenueRequest { AddressLine = "1 Main Rd", City = "Pretoria", Province = "Gauteng", Latitude = -25.7479m, Longitude = 28.2293m }
    };

    public static async Task<EventDto> CreateEvent(this HttpClient organiser, string name = "Test Run")
    {
        var resp = await organiser.PostJson("/api/events", NewEvent(name));
        resp.EnsureSuccessStatusCode();
        return (await resp.Read<EventDto>())!;
    }

    public static async Task<CategoryDto> CreateCategory(this HttpClient organiser, int eventId, int max = 100)
    {
        var resp = await organiser.PostJson($"/api/events/{eventId}/categories",
            new CategoryRequest { Name = "10km", DistanceKm = 10, MaxParticipants = max, EntryFee = 150 });
        resp.EnsureSuccessStatusCode();
        return (await resp.Read<CategoryDto>())!;
    }
}
