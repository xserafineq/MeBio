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
    private readonly IAuthService _authService;
    private readonly IFaceRecognitionService _faceService;
    private readonly IFingerprintRecognitionService _fingerprintService;

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
    [NotifyPropertyChangedFor(nameof(FaceModalImage))]
    private ImageSource? _facePreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FaceModalImage))]
    private ImageSource? _facePreviewWithLandmarks;

    [ObservableProperty]
    private bool _showLandmarks;

    partial void OnShowLandmarksChanged(bool value)
    {
        OnPropertyChanged(nameof(FaceModalImage));
    }

    public ImageSource? FaceModalImage => ShowLandmarks ? FacePreviewWithLandmarks : FacePreview;

    [ObservableProperty]
    private bool _hasFingerprintTemplate;

    [ObservableProperty]
    private double? _fingerprintQualityScore;

    [ObservableProperty]
    private string _fingerprintCapturedText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FingerprintModalImage))]
    private ImageSource? _fingerprintPreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FingerprintModalImage))]
    private ImageSource? _fingerprintPreviewWithMinutiae;

    [ObservableProperty]
    private bool _showMinutiae;

    partial void OnShowMinutiaeChanged(bool value)
    {
        OnPropertyChanged(nameof(FingerprintModalImage));
    }

    public ImageSource? FingerprintModalImage => ShowMinutiae ? FingerprintPreviewWithMinutiae : FingerprintPreview;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isFaceModalOpen;

    [ObservableProperty]
    private bool _isFingerprintModalOpen;

    public ProfileViewModel(
        IUserService userService,
        IBiometricStatsService statsService,
        ISessionService session,
        INavigationService navigation,
        IAuthService authService,
        IFaceRecognitionService faceService,
        IFingerprintRecognitionService fingerprintService)
    {
        _userService = userService;
        _statsService = statsService;
        _session = session;
        _navigation = navigation;
        _authService = authService;
        _faceService = faceService;
        _fingerprintService = fingerprintService;
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

            FacePreviewWithLandmarks = overview.FacePreviewImage is { Length: > 0 } bytes2
                ? ImageSource.FromStream(() => new MemoryStream(_faceService.DrawLandmarks(bytes2)))
                : null;

            HasFingerprintTemplate = overview.HasFingerprintTemplate;
            FingerprintQualityScore = overview.FingerprintQualityScore;
            FingerprintCapturedText = overview.FingerprintCapturedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—";
            FingerprintPreview = overview.FingerprintPreviewImage is { Length: > 0 } fpBytes
                ? ImageSource.FromStream(() => new MemoryStream(fpBytes))
                : null;

            FingerprintPreviewWithMinutiae = overview.FingerprintPreviewImage is { Length: > 0 } fpImage
                && overview.FingerprintTemplateData is { Length: > 0 } fpTemplate
                ? ImageSource.FromStream(() => new MemoryStream(_fingerprintService.DrawMinutiae(fpImage, fpTemplate)))
                : null;
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

        FaceCaptureHelper.BeginEnrollment();
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
    private async Task UpdateFingerprintAsync()
    {
        var userId = _session.CurrentUser?.Id;
        if (userId is null) return;

        FingerprintCaptureHelper.BeginEnrollment();
        if (!await _navigation.TryGoToFingerprintCaptureAsync())
        {
            StatusMessage = "Brak kamery.";
            return;
        }

        var capture = await FingerprintCaptureHelper.CaptureAsync();
        if (capture is null) return;

        var (success, message) = await _userService.SetFingerprintTemplateAsync(userId.Value, capture.ImageBytes);
        StatusMessage = message;

        if (success)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        await _navigation.GoToLoginAsync();
    }

    [RelayCommand]
    private void OpenFaceModal()
    {
        if (HasFaceTemplate)
        {
            IsFaceModalOpen = true;
        }
    }

    [RelayCommand]
    private void CloseFaceModal()
    {
        IsFaceModalOpen = false;
    }

    [RelayCommand]
    private void OpenFingerprintModal()
    {
        if (HasFingerprintTemplate)
            IsFingerprintModalOpen = true;
    }

    [RelayCommand]
    private void CloseFingerprintModal()
    {
        IsFingerprintModalOpen = false;
    }
}
