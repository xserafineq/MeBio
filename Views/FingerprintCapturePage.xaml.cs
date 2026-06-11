using MeBio.ViewModels;

namespace MeBio.Views;

public partial class FingerprintCapturePage : ContentPage
{
    private readonly FingerprintCaptureViewModel _vm;

    public FingerprintCapturePage(FingerprintCaptureViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeCameraAsync(CameraHost);
    }

    protected override async void OnDisappearing()
    {
        await _vm.StopCameraAsync();
        if (_vm.Camera is CommunityToolkit.Maui.Views.CameraView cameraView)
        {
            cameraView.Handler?.DisconnectHandler();
            (cameraView as IDisposable)?.Dispose();
        }
        CameraHost.Children.Clear();
        base.OnDisappearing();
    }
}
