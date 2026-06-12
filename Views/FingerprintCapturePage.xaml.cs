using MeBio.ViewModels;

namespace MeBio.Views;

public partial class FingerprintCapturePage : ContentPage
{
    public FingerprintCapturePage(FingerprintCaptureViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is FingerprintCaptureViewModel vm)
            vm.Initialize();
    }
}
