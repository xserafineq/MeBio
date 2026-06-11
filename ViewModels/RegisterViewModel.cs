using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Helpers;
using MeBio.Models;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _ageText = string.Empty;

    [ObservableProperty]
    private Gender _gender = Gender.Male;

    [ObservableProperty]
    private bool _useFace;

    [ObservableProperty]
    private bool _useVoice;

    [ObservableProperty]
    private bool _useFingerprint;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public IList<Gender> Genders { get; } = [Gender.Male, Gender.Female, Gender.Other];

    public RegisterViewModel(IAuthService authService, INavigationService navigation)
    {
        _authService = authService;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;

        if (Password != ConfirmPassword)
        {
            StatusMessage = "Hasła nie są identyczne.";
            return;
        }

        if (!int.TryParse(AgeText, out var age))
        {
            StatusMessage = "Podaj prawidłowy wiek.";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            byte[]? faceImage = null;
            IReadOnlyList<byte[]>? voiceSamples = null;
            byte[]? fingerprintImage = null;

            if (UseFace)
            {
                FaceCaptureHelper.BeginEnrollment();
                if (!await _navigation.TryGoToFaceCaptureAsync())
                {
                    StatusMessage = "Rejestracja twarzy anulowana — brak kamery.";
                    return;
                }

                var capture = await FaceCaptureHelper.CaptureAsync();
                if (capture is null)
                {
                    StatusMessage = "Rejestracja twarzy anulowana.";
                    return;
                }

                faceImage = capture.ImageBytes;
            }

            if (UseVoice)
            {
                VoiceCaptureHelper.BeginEnrollment();
                if (!await _navigation.TryGoToVoiceCaptureAsync())
                {
                    StatusMessage = "Rejestracja głosu anulowana — brak mikrofonu.";
                    return;
                }

                var voiceCapture = await VoiceCaptureHelper.CaptureAsync();
                if (voiceCapture is null)
                {
                    StatusMessage = "Rejestracja głosu anulowana.";
                    return;
                }

                voiceSamples = voiceCapture.WavSamples;
            }

            if (UseFingerprint)
            {
                FingerprintCaptureHelper.BeginEnrollment();
                if (!await _navigation.TryGoToFingerprintCaptureAsync())
                {
                    StatusMessage = "Rejestracja odcisku anulowana — brak kamery.";
                    return;
                }

                var fingerprintCapture = await FingerprintCaptureHelper.CaptureAsync();
                if (fingerprintCapture is null)
                {
                    StatusMessage = "Rejestracja odcisku anulowana.";
                    return;
                }

                fingerprintImage = fingerprintCapture.ImageBytes;
            }

            var (success, message) = await _authService.RegisterAsync(
                FirstName, LastName, Email, Password, age, Gender, faceImage, voiceSamples, fingerprintImage);
            StatusMessage = message;

            if (success)
                await _navigation.GoBackAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoBackAsync() => await _navigation.GoBackAsync();
}
