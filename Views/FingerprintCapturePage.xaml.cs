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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _ = ReleaseCameraSafelyAsync();
    }

    private async Task ReleaseCameraSafelyAsync()
    {
        try
        {
            await _vm.StopCameraAsync();
            CameraHost.Children.Clear();
        }
        catch
        {
            // Strona jest niszczona podczas nawigacji — ignoruj błędy kamery.
        }
    }
}
