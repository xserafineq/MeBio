namespace MeBio.Services;

public interface ICameraAvailabilityService
{
    Task<(bool Available, string Message)> CheckAsync();
}

public class CameraAvailabilityService : ICameraAvailabilityService
{
    public async Task<(bool Available, string Message)> CheckAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Camera>();

            if (status is PermissionStatus.Denied or PermissionStatus.Restricted)
                return (false, "Brak uprawnień do kamery. Użyj logowania hasłem.");

#if WINDOWS
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                Windows.Devices.Enumeration.DeviceClass.VideoCapture);
            if (devices.Count == 0)
                return (false, "Nie wykryto kamery. Podłącz kamerę lub zaloguj się hasłem.");
#endif

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Kamera niedostępna: {ex.Message}");
        }
    }
}
