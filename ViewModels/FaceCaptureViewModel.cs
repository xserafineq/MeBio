using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Helpers;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class FaceCaptureViewModel : ObservableObject
{
    private readonly IFaceRecognitionService _faceService;
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

    public CameraView? Camera { get; private set; }

    public FaceCaptureViewModel(IFaceRecognitionService faceService, INavigationService navigation)
    {
        _faceService = faceService;
        _navigation = navigation;
    }

    public async Task InitializeCameraAsync(Grid cameraHost)
    {
        HasCamera = false;
        IsCameraReady = false;
        IsBusy = true;
        StatusMessage = "Uruchamianie kamery…";

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

            // Czekamy chwilę, aż handler kontrolki zostanie utworzony przez MAUI
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
            StatusMessage = "Ustaw twarz w kadrze i zrób zdjęcie.";
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
                StatusMessage = $"Jakość: {QualityScore:F0}%";

                if (QualityScore < 30)
                {
                    StatusMessage = "Zbyt niska jakość — popraw oświetlenie.";
                    return;
                }

                var template = _faceService.ExtractTemplate(bytes);
                FaceCaptureHelper.Complete(new FaceCaptureResult(bytes, template, QualityScore));
                await _navigation.GoBackAsync();
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
