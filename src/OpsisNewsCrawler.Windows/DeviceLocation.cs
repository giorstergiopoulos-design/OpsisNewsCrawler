using Windows.Devices.Geolocation;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Opt-in approximate location from the Windows Location service. Called only after an explicit click and a confirmation in the settings.
/// Reduced accuracy on purpose (weather does not need more), rounded to 2 decimals (about 1 km) before it leaves this class, never logged.
/// </summary>
internal static class DeviceLocation
{
    public static async Task<(double Lat, double Lon)?> GetApproximateAsync()
    {
        try
        {
            if (await Geolocator.RequestAccessAsync() != GeolocationAccessStatus.Allowed) return null;
            var locator = new Geolocator { DesiredAccuracyInMeters = 5000 };
            var pos = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10));
            var p = pos.Coordinate.Point.Position;
            if (Math.Abs(p.Latitude) > 90 || Math.Abs(p.Longitude) > 180) return null;
            return (Math.Round(p.Latitude, 2), Math.Round(p.Longitude, 2));
        }
        catch (Exception) { return null; }
    }
}
