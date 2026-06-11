using MeBio.ViewModels;

namespace MeBio.Views;

public partial class FaceCapturePage : ContentPage
{
    private readonly FaceCaptureViewModel _vm;

    public FaceCapturePage(FaceCaptureViewModel vm)
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
