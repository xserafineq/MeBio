using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Helpers;
using MeBio.Models;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly IBiometricStatsService _statsService;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _ageText = string.Empty;

    [ObservableProperty]
    private string _genderText = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _hasFaceTemplate;

    [ObservableProperty]
    private double? _faceQualityScore;

    [ObservableProperty]
    private string _faceCapturedText = string.Empty;

    [ObservableProperty]
    private ImageSource? _facePreview;

    [ObservableProperty]
    private bool _hasVoiceTemplate;

    [ObservableProperty]
    private double? _voiceQualityScore;

    [ObservableProperty]
    private string _voiceCapturedText = string.Empty;

    [ObservableProperty]
    private string _voiceFilePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ProfileViewModel(
        IUserService userService,
        IBiometricStatsService statsService,
        ISessionService session,
        INavigationService navigation)
    {
        _userService = userService;
        _statsService = statsService;
        _session = session;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        var userId = _session.CurrentUser?.Id;
        if (userId is null) return;

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var user = await _userService.GetByIdAsync(userId.Value);
            if (user is null) return;

            FirstName = user.FirstName;
            LastName = user.LastName;
            Email = user.Email;
            AgeText = user.Age.ToString();
            GenderText = GenderLabels.ToDisplayName(user.Gender);

            var overview = await _statsService.GetUserOverviewAsync(userId.Value);
            HasFaceTemplate = overview.HasFaceTemplate;
            FaceQualityScore = overview.FaceQualityScore;
            FaceCapturedText = overview.FaceCapturedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—";
            FacePreview = overview.FacePreviewImage is { Length: > 0 } bytes
                ? ImageSource.FromStream(() => new MemoryStream(bytes))
                : null;

            HasVoiceTemplate = overview.HasVoiceTemplate;
            VoiceQualityScore = overview.VoiceQualityScore;
            VoiceCapturedText = overview.VoiceCapturedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—";
            VoiceFilePath = overview.VoiceAudioPath ?? string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var userId = _session.CurrentUser?.Id;
        if (userId is null) return;

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var (success, message) = await _userService.UpdateProfileAsync(
                userId.Value, Email, NewPassword, ConfirmPassword);
            StatusMessage = message;

            if (success)
            {
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
                await LoadAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UpdateFaceAsync()
    {
        var userId = _session.CurrentUser?.Id;
        if (userId is null) return;

        if (!await _navigation.TryGoToFaceCaptureAsync())
        {
            StatusMessage = "Brak kamery.";
            return;
        }

        var capture = await FaceCaptureHelper.CaptureAsync();
        if (capture is null) return;

        var (success, message) = await _userService.SetFaceTemplateAsync(userId.Value, capture.ImageBytes);
        StatusMessage = message;

        if (success)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task UpdateVoiceAsync()
    {
        var userId = _session.CurrentUser?.Id;
        if (userId is null) return;

        if (!await _navigation.TryGoToVoiceCaptureAsync())
        {
            StatusMessage = "Brak mikrofonu.";
            return;
        }

        var capture = await VoiceCaptureHelper.CaptureAsync();
        if (capture is null) return;

        var (success, message) = await _userService.SetVoiceTemplateAsync(userId.Value, capture.WavBytes);
        StatusMessage = message;

        if (success)
            await LoadAsync();
    }
}
