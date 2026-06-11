using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Helpers;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class FaceCaptureViewModel : ObservableObject
{
    private readonly IFaceRecognitionService _faceService;
    private readonly IAuthService _authService;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _statusMessage = "Uruchamianie kamery…";

    [ObservableProperty]
    private double _qualityScore;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasCamera;

    [ObservableProperty]
    private bool _isCameraReady;

    [ObservableProperty]
    private bool _showAuthResult;

    [ObservableProperty]
    private string _authResultMessage = string.Empty;

    public bool IsLoginMode => FaceCaptureHelper.Mode == FaceCaptureMode.Login;
    public CameraView? Camera { get; private set; }

    public FaceCaptureViewModel(
        IFaceRecognitionService faceService,
        IAuthService authService,
        INavigationService navigation)
    {
        _faceService = faceService;
        _authService = authService;
        _navigation = navigation;
    }

    public async Task InitializeCameraAsync(Grid cameraHost)
    {
        HasCamera = false;
        IsCameraReady = false;
        IsBusy = true;
        StatusMessage = IsLoginMode
            ? "Ustaw twarz w kadrze i zrób zdjęcie do logowania."
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

            int retries = 0;
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
            StatusMessage = IsLoginMode
                ? "Ustaw twarz w kadrze i zrób zdjęcie."
                : "Ustaw twarz w kadrze i zrób zdjęcie.";
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

                QualityScore = _faceService.ComputeQualityScore(bytes);

                if (QualityScore < FaceRecognitionDefaults.MinQualityScore)
                {
                    StatusMessage = QualityScore < 1
                        ? "Nie wykryto twarzy — przybliż twarz do kamery i ustaw ją w środku."
                        : $"Zbyt ciemne lub rozmyte ({QualityScore:F0}%) — popraw oświetlenie.";
                    ShowAuthResult = true;
                    AuthResultMessage = QualityScore < 1
                        ? "Na zdjęciu nie widać twarzy. Przybliż twarz do kamery."
                        : $"Jakość: {QualityScore:F0}% (min. {FaceRecognitionDefaults.MinQualityScore:F0}%). Ustaw twarz w środku i doświetl kadr.";
                    return;
                }

                if (IsLoginMode)
                {
                    var email = FaceCaptureHelper.LoginEmail;
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        StatusMessage = "Brak adresu email do logowania.";
                        return;
                    }

                    StatusMessage = "Weryfikacja twarzy…";
                    var auth = await _authService.LoginWithFaceAsync(bytes, email);

                    if (auth.Success)
                    {
                        StatusMessage = auth.Message;
                        await StopCameraAsync();
                        FaceCaptureHelper.Cancel();
                        await _navigation.GoToMainAsync();
                        return;
                    }

                    StatusMessage = "Spróbuj ponownie — ustaw twarz w kadrze.";
                    ShowAuthResult = true;
                    AuthResultMessage = BiometricAuthFeedback.FormatFailure(auth);
                    return;
                }

                try
                {
                    var template = _faceService.ExtractTemplate(bytes);
                    FaceCaptureHelper.Complete(new FaceCaptureResult(bytes, template, QualityScore));
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
        FaceCaptureHelper.Cancel();
        await _navigation.GoBackAsync();
    }
}
