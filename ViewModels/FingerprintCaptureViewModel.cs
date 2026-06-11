using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Views;
using MeBio.Helpers;
using MeBio.Services;
using Microsoft.Maui.Controls;

namespace MeBio.ViewModels;

public partial class FingerprintCaptureViewModel : ObservableObject
{
    private readonly IFingerprintRecognitionService _fingerprintService;
    private readonly IAuthService _authService;
    private readonly INavigationService _navigation;

    [ObservableProperty] private string _statusMessage = "Uruchamianie kamery…";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasCamera;
    [ObservableProperty] private bool _isCameraReady;
    [ObservableProperty] private bool _showAuthResult;
    [ObservableProperty] private string _authResultMessage = string.Empty;
    [ObservableProperty] private double _qualityScore;

    public CameraView? Camera { get; private set; }

    private bool IsLoginMode => FingerprintCaptureHelper.Mode == FingerprintCaptureMode.Login;

    public FingerprintCaptureViewModel(
        IFingerprintRecognitionService fingerprintService,
        IAuthService authService,
        INavigationService navigation)
    {
        _fingerprintService = fingerprintService;
        _authService = authService;
        _navigation = navigation;
    }

    public async Task InitializeCameraAsync(Grid cameraHost)
    {
        HasCamera = false;
        IsCameraReady = false;
        IsBusy = true;
        StatusMessage = IsLoginMode
            ? "Ustaw palec z odciskiem w środku kadru i zrób zdjęcie."
            : "Uruchamianie kamery…";

        try
        {
            cameraHost.Children.Clear();
            Camera = null;

            var camera = new CameraView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };

            cameraHost.Children.Add(camera);
            Camera = camera;

            var retries = 0;
            while (camera.Handler == null && retries < 20)
            {
                await Task.Delay(50);
                retries++;
            }

            var cameras = await camera.GetAvailableCameras(CancellationToken.None);
            if (cameras is null || !cameras.Any())
            {
                cameraHost.Children.Clear();
                Camera = null;
                StatusMessage = "Nie wykryto kamery.";
                return;
            }

            await camera.StartCameraPreview(CancellationToken.None);
            HasCamera = true;
            IsCameraReady = true;
            StatusMessage = "Ustaw palec z odciskiem w środku kadru i zrób zdjęcie.";
        }
        catch (Exception ex)
        {
            cameraHost.Children.Clear();
            Camera = null;
            HasCamera = false;
            IsCameraReady = false;
            StatusMessage = $"Kamera niedostępna: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task StopCameraAsync()
    {
        try
        {
            if (Camera is not null && IsCameraReady)
                Camera.StopCameraPreview();
        }
        catch
        {
            // ignore
        }
        finally
        {
            IsCameraReady = false;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CaptureAsync()
    {
        if (IsBusy || Camera is null || !IsCameraReady)
            return;

        IsBusy = true;
        ShowAuthResult = false;
        AuthResultMessage = string.Empty;

        try
        {
            var stream = await Camera.CaptureImage(CancellationToken.None);
            if (stream is null)
            {
                StatusMessage = "Nie udało się zrobić zdjęcia.";
                return;
            }

            await using (stream)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var bytes = ms.ToArray();

                QualityScore = _fingerprintService.ComputeQualityScore(bytes);

                if (QualityScore < FingerprintRecognitionDefaults.MinQualityScore)
                {
                    StatusMessage = QualityScore < 1
                        ? "Nie wykryto odcisku palca — ustaw palec w środku kadru."
                        : $"Zbyt ciemne lub rozmyte ({QualityScore:F0}%) — popraw oświetlenie.";
                    ShowAuthResult = true;
                    AuthResultMessage = QualityScore < 1
                        ? "Na zdjęciu nie widać odcisku. Ustaw palec na jasnym tle i przybliż do kamery."
                        : $"Jakość: {QualityScore:F0}% (min. {FingerprintRecognitionDefaults.MinQualityScore:F0}%). Ustaw palec w środku i doświetl kadr.";
                    return;
                }

                if (IsLoginMode)
                {
                    var email = FingerprintCaptureHelper.LoginEmail;
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        StatusMessage = "Brak adresu email do logowania.";
                        return;
                    }

                    StatusMessage = "Weryfikacja odcisku…";
                    var auth = await _authService.LoginWithFingerprintAsync(bytes, email);

                    if (auth.Success)
                    {
                        StatusMessage = auth.Message;
                        await _navigation.GoToMainAsync();
                        FingerprintCaptureHelper.Cancel();
                        return;
                    }

                    StatusMessage = "Spróbuj ponownie — ustaw palec w kadrze.";
                    ShowAuthResult = true;
                    AuthResultMessage = BiometricAuthFeedback.FormatFailure(auth);
                    return;
                }

                try
                {
                    var template = _fingerprintService.ExtractTemplate(bytes);
                    FingerprintCaptureHelper.Complete(new FingerprintCaptureResult(bytes, template, QualityScore));
                    await _navigation.GoBackAsync();
                }
                catch (InvalidOperationException ex)
                {
                    StatusMessage = ex.Message;
                    ShowAuthResult = true;
                    AuthResultMessage = ex.Message;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd kamery: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        FingerprintCaptureHelper.Cancel();
        await _navigation.GoBackAsync();
    }
}
