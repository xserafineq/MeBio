using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Helpers;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(IAuthService authService, INavigationService navigation)
    {
        _authService = authService;
        _navigation = navigation;
    }

    private bool TryValidateEmail(out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(Email))
        {
            message = "Podaj email przed logowaniem.";
            return false;
        }

        if (!ValidationHelper.IsValidEmail(Email.Trim()))
        {
            message = "Podaj prawidłowy adres email.";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task LoginWithPasswordAsync()
    {
        if (IsBusy) return;
        if (!TryValidateEmail(out var validationMessage))
        {
            StatusMessage = validationMessage;
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var (success, message, _) = await _authService.LoginWithPasswordAsync(Email, Password);
            StatusMessage = message;
            if (success)
                await _navigation.GoToMainAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginWithFaceAsync()
    {
        if (IsBusy) return;
        if (!TryValidateEmail(out var validationMessage))
        {
            StatusMessage = validationMessage;
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            if (!await _navigation.TryGoToFaceCaptureAsync())
                return;

            var result = await FaceCaptureHelper.CaptureAsync();

            if (result is null)
            {
                StatusMessage = "Anulowano.";
                return;
            }

            var (success, message, _) = await _authService.LoginWithFaceAsync(result.ImageBytes, Email);
            StatusMessage = message;
            if (success)
                await _navigation.GoToMainAsync();
        }
        catch (Exception ex)
        {
            FaceCaptureHelper.Cancel();
            StatusMessage = $"Błąd logowania twarzą: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginWithVoiceAsync()
    {
        if (IsBusy) return;
        if (!TryValidateEmail(out var validationMessage))
        {
            StatusMessage = validationMessage;
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            if (!await _navigation.TryGoToVoiceCaptureAsync())
                return;

            var result = await VoiceCaptureHelper.CaptureAsync();

            if (result is null)
            {
                StatusMessage = "Anulowano.";
                return;
            }

            var (success, message, _) = await _authService.LoginWithVoiceAsync(result.WavBytes, Email);
            StatusMessage = message;
            if (success)
                await _navigation.GoToMainAsync();
        }
        catch (Exception ex)
        {
            VoiceCaptureHelper.Cancel();
            StatusMessage = $"Błąd logowania głosem: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoToRegisterAsync() => await _navigation.GoToRegisterAsync();
}
