namespace MeBio.Services;

public interface IMicrophoneAvailabilityService
{
    Task<(bool Available, string Message)> CheckAsync();
}

public class MicrophoneAvailabilityService : IMicrophoneAvailabilityService
{
    public async Task<(bool Available, string Message)> CheckAsync()
    {
        try
        {
#if WINDOWS
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                Windows.Devices.Enumeration.DeviceClass.AudioCapture);
            if (devices.Count == 0)
                return (false, "Nie wykryto mikrofonu. Podłącz urządzenie lub zaloguj się hasłem.");

            return (true, string.Empty);
#else
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Microphone>();

            if (status is PermissionStatus.Denied or PermissionStatus.Restricted)
                return (false, "Brak uprawnień do mikrofonu. Użyj logowania hasłem.");

            return (true, string.Empty);
#endif
        }
        catch (Exception ex)
        {
            return (false, $"Mikrofon niedostępny: {ex.Message}");
        }
    }
}
