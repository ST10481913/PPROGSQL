using System.Globalization;
using System.Text.Json;

namespace RaceDay.Api.Services;

/// <summary>Fetches a daily forecast from the free Open-Meteo API (no API key needed).</summary>
public class WeatherService(HttpClient http)
{
    public async Task<JsonElement?> GetForecastAsync(decimal lat, decimal lon, DateTime date)
    {
        var d = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = string.Create(CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={lat}&longitude={lon}&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max&timezone=auto&start_date={d}&end_date={d}");
        try
        {
            using var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch (HttpRequestException) { return null; }
    }
}
