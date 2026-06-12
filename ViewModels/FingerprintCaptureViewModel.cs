using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Helpers;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class FingerprintCaptureViewModel : ObservableObject
{
    private readonly IFingerprintRecognitionService _fingerprintService;
    private readonly IAuthService _authService;
    private readonly INavigationService _navigation;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _showAuthResult;
    [ObservableProperty] private string _authResultMessage = string.Empty;

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

    public void Initialize()
    {
        StatusMessage = IsLoginMode
            ? "Wybierz plik z obrazem odcisku palca."
            : "Wybierz plik ze skanem lub zdjęciem odcisku palca.";
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
            var qualityScore = _fingerprintService.ComputeQualityScore(bytes);

            if (qualityScore < FingerprintRecognitionDefaults.MinQualityScore)
            {
                StatusMessage = qualityScore < 1
                    ? "Nie wykryto odcisku palca — upewnij się, że plik przedstawia odcisk."
                    : $"Zbyt ciemne lub rozmyte ({qualityScore:F0}%) — wybierz lepszy plik.";
                ShowAuthResult = true;
                AuthResultMessage = qualityScore < 1
                    ? "Na obrazie nie widać odcisku. Wybierz prawidłowy plik."
                    : $"Jakość: {qualityScore:F0}% (min. {FingerprintRecognitionDefaults.MinQualityScore:F0}%).";
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
                    FingerprintCaptureHelper.Cancel();
                    await _navigation.GoToMainAsync();
                    return;
                }

                StatusMessage = "Spróbuj ponownie — wybierz inny plik z odciskiem.";
                ShowAuthResult = true;
                AuthResultMessage = BiometricAuthFeedback.FormatFailure(auth);
                return;
            }

            try
            {
                var template = _fingerprintService.ExtractTemplate(bytes);
                FingerprintCaptureHelper.Complete(new FingerprintCaptureResult(bytes, template, qualityScore));
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
