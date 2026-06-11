using MeBio.ViewModels;

namespace MeBio.Views;

public partial class VoiceCapturePage : ContentPage
{
    public VoiceCapturePage(VoiceCaptureViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
