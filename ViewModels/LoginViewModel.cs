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

        StatusMessage = string.Empty;
        FaceCaptureHelper.BeginLogin(Email);
        await _navigation.TryGoToFaceCaptureAsync();
    }

    [RelayCommand]
    private async Task LoginWithFingerprintAsync()
    {
        if (IsBusy) return;
        if (!TryValidateEmail(out var validationMessage))
        {
            StatusMessage = validationMessage;
            return;
        }

        StatusMessage = string.Empty;
        FingerprintCaptureHelper.BeginLogin(Email);
        await _navigation.TryGoToFingerprintCaptureAsync();
    }

    [RelayCommand]
    private async Task GoToRegisterAsync() => await _navigation.GoToRegisterAsync();
}
