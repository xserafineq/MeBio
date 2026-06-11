using MeBio.Helpers;
using MeBio.Views;

namespace MeBio.Services;

public interface INavigationService
{
    Task GoToRegisterAsync();
    Task<bool> TryGoToFaceCaptureAsync();
    Task<bool> TryGoToVoiceCaptureAsync();
    Task<bool> TryGoToFingerprintCaptureAsync();
    Task GoBackAsync();
    Task GoToMainAsync();
    Task GoToLoginAsync();
    Task GoToUserEditAsync(int userId);
}

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly ICameraAvailabilityService _cameraAvailability;
    private readonly IMicrophoneAvailabilityService _microphoneAvailability;

    public NavigationService(
        IServiceProvider services,
        ICameraAvailabilityService cameraAvailability,
        IMicrophoneAvailabilityService microphoneAvailability)
    {
        _services = services;
        _cameraAvailability = cameraAvailability;
        _microphoneAvailability = microphoneAvailability;
    }

    public async Task GoToRegisterAsync()
    {
        var page = _services.GetRequiredService<RegisterPage>();
        await GetNavigation().PushAsync(page);
    }

    public async Task<bool> TryGoToFaceCaptureAsync()
    {
        var (available, message) = await _cameraAvailability.CheckAsync();
        if (!available)
        {
            await AlertHelper.ShowAsync("Kamera", message);
            return false;
        }

        try
        {
            var page = _services.GetRequiredService<FaceCapturePage>();
            await GetNavigation().PushAsync(page);
            return true;
        }
        catch (Exception ex)
        {
            await AlertHelper.ShowAsync("Kamera", $"Nie można uruchomić kamery: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TryGoToVoiceCaptureAsync()
    {
        var (available, message) = await _microphoneAvailability.CheckAsync();
        if (!available)
        {
            await AlertHelper.ShowAsync("Mikrofon", message);
            return false;
        }

        try
        {
            var page = _services.GetRequiredService<VoiceCapturePage>();
            await GetNavigation().PushAsync(page);
            return true;
        }
        catch (Exception ex)
        {
            await AlertHelper.ShowAsync("Mikrofon", $"Nie można uruchomić nagrywania: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TryGoToFingerprintCaptureAsync()
    {
        var (available, message) = await _cameraAvailability.CheckAsync();
        if (!available)
        {
            await AlertHelper.ShowAsync("Kamera", message);
            return false;
        }

        try
        {
            var page = _services.GetRequiredService<FingerprintCapturePage>();
            await GetNavigation().PushAsync(page);
            return true;
        }
        catch (Exception ex)
        {
            await AlertHelper.ShowAsync("Kamera", $"Nie można uruchomić kamery: {ex.Message}");
            return false;
        }
    }

    public Task GoBackAsync()
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is Shell)
            return MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));

        return MainThread.InvokeOnMainThreadAsync(() => GetNavigation().PopAsync());
    }

    public Task GoToMainAsync() =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            var shell = _services.GetRequiredService<AppShell>();
            if (Application.Current?.Windows.FirstOrDefault() is Window window)
                window.Page = shell;
        });

    public Task GoToLoginAsync() =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            var login = _services.GetRequiredService<LoginPage>();
            NavigationPage.SetHasNavigationBar(login, false);
            if (Application.Current?.Windows.FirstOrDefault() is Window window)
                window.Page = new NavigationPage(login);
        });

    public Task GoToUserEditAsync(int userId) =>
        Shell.Current.GoToAsync($"{nameof(UserEditPage)}?userId={userId}");

    private static INavigation GetNavigation()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page
            ?? throw new InvalidOperationException("Brak aktywnej strony.");

        if (page is NavigationPage navPage)
            return navPage.Navigation;

        if (page is Shell shell && shell.CurrentPage?.Navigation is INavigation shellNav)
            return shellNav;

        return page.Navigation;
    }
}
