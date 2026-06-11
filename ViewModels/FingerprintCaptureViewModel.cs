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

                await ProcessImageBytesAsync(bytes);
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
    private async Task PickFromFileAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ShowAuthResult = false;
        AuthResultMessage = string.Empty;

        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.image" } },
                    { DevicePlatform.Android, new[] { "image/*" } },
                    { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff" } },
                    { DevicePlatform.macOS, new[] { "public.image" } },
                });

            var options = new PickOptions
            {
                PickerTitle = "Wybierz obraz odcisku palca",
                FileTypes = customFileType
            };

            var result = await FilePicker.Default.PickAsync(options);
            if (result == null)
            {
                StatusMessage = "Nie wybrano pliku.";
                return;
            }

            await using var stream = await result.OpenReadAsync();
            byte[] bytes;

            var extension = Path.GetExtension(result.FileName).ToLowerInvariant();
            if (extension == ".tif" || extension == ".tiff")
            {
                try
                {
                    var image = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(stream);
                    using var msPng = new MemoryStream();
                    image.Save(msPng, ImageFormat.Png);
                    bytes = msPng.ToArray();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Błąd konwersji TIFF: {ex.Message}";
                    return;
                }
            }
            else
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            await ProcessImageBytesAsync(bytes);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd podczas wybierania pliku: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ProcessImageBytesAsync(byte[] bytes)
    {
        try
        {
            QualityScore = _fingerprintService.ComputeQualityScore(bytes);

            if (QualityScore < FingerprintRecognitionDefaults.MinQualityScore)
            {
                StatusMessage = QualityScore < 1
                    ? "Nie wykryto odcisku palca — upewnij się, że obrazek przedstawia odcisk palca."
                    : $"Zbyt ciemne lub rozmyte ({QualityScore:F0}%) — popraw jakość obrazu.";
                ShowAuthResult = true;
                AuthResultMessage = QualityScore < 1
                    ? "Na zdjęciu nie widać odcisku. Upewnij się, że wgrałeś prawidłowy plik."
                    : $"Jakość: {QualityScore:F0}% (min. {FingerprintRecognitionDefaults.MinQualityScore:F0}%). Popraw oświetlenie/ostrość pliku.";
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
                    await StopCameraAsync();
                    FingerprintCaptureHelper.Cancel();
                    await _navigation.GoToMainAsync();
                    return;
                }

                StatusMessage = "Spróbuj ponownie — wgraj inny plik z odciskiem.";
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
        catch (Exception ex)
        {
            StatusMessage = $"Błąd przetwarzania obrazu: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        FingerprintCaptureHelper.Cancel();
        await _navigation.GoBackAsync();
    }
}
