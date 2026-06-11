using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Helpers;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class VoiceCaptureViewModel : ObservableObject
{
    private readonly IVoiceRecognitionService _voiceService;
    private readonly IAuthService _authService;
    private readonly IAudioRecordingService _audioRecording;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _statusMessage = "Przygotuj się do nagrania.";

    [ObservableProperty]
    private string _countdownText = string.Empty;

    [ObservableProperty]
    private string _sampleProgressText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _showAuthResult;

    [ObservableProperty]
    private string _authResultMessage = string.Empty;

    public string Phrase => VoiceRecognitionDefaults.Phrase;
    public bool IsEnrollment => VoiceCaptureHelper.Mode == VoiceCaptureMode.Enrollment;
    public bool IsLoginMode => VoiceCaptureHelper.Mode == VoiceCaptureMode.Single;

    public VoiceCaptureViewModel(
        IVoiceRecognitionService voiceService,
        IAuthService authService,
        IAudioRecordingService audioRecording,
        INavigationService navigation)
    {
        _voiceService = voiceService;
        _authService = authService;
        _audioRecording = audioRecording;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task RecordAsync()
    {
        if (IsBusy || IsRecording)
            return;

        IsBusy = true;
        IsRecording = true;
        ShowAuthResult = false;
        AuthResultMessage = string.Empty;
        var samples = new List<byte[]>();
        var qualities = new List<double>();
        var required = VoiceCaptureHelper.RequiredSamples;

        try
        {
            for (var sampleIndex = 1; sampleIndex <= required; sampleIndex++)
            {
                if (IsEnrollment)
                {
                    SampleProgressText = $"Próba {sampleIndex}/{required}";
                    StatusMessage = "Powtórz frazę — każda próba poprawia rozpoznawanie.";
                }
                else
                {
                    SampleProgressText = string.Empty;
                    StatusMessage = "Przygotuj się do nagrania.";
                }

                for (var i = 3; i >= 1; i--)
                {
                    CountdownText = i.ToString();
                    StatusMessage = $"Powiedz: „{Phrase}”";
                    await Task.Delay(1000);
                }

                CountdownText = "●";
                StatusMessage = $"Mów teraz: „{Phrase}”";

                var wavBytes = await _audioRecording.RecordWavAsync(
                    VoiceRecognitionDefaults.RecordingDurationSeconds);

                var quality = _voiceService.ComputeQualityScore(wavBytes);
                qualities.Add(quality);

                var minQuality = IsLoginMode
                    ? VoiceRecognitionDefaults.MinLoginQualityScore
                    : VoiceRecognitionDefaults.MinQualityScore;

                if (quality < minQuality)
                {
                    StatusMessage = $"Próba {sampleIndex}: zbyt niska jakość ({quality:F0}%) — mów głośniej i bliżej mikrofonu.";
                    if (IsLoginMode)
                    {
                        ShowAuthResult = true;
                        AuthResultMessage = $"Jakość próbki: {quality:F0}% (wymagane min. {minQuality:F0}%).";
                    }
                    sampleIndex--;
                    continue;
                }

                samples.Add(wavBytes);
                StatusMessage = $"Próba {sampleIndex}/{required}: jakość {quality:F0}%";

                if (sampleIndex < required)
                    await Task.Delay(800);
            }

            if (IsLoginMode)
            {
                var email = VoiceCaptureHelper.LoginEmail;
                if (string.IsNullOrWhiteSpace(email))
                {
                    StatusMessage = "Brak adresu email do logowania.";
                    return;
                }

                StatusMessage = "Weryfikacja głosu…";
                var auth = await _authService.LoginWithVoiceAsync(samples[0], email);

                if (auth.Success)
                {
                    StatusMessage = auth.Message;
                    await _navigation.GoToMainAsync();
                    VoiceCaptureHelper.Cancel();
                    return;
                }

                StatusMessage = "Spróbuj nagrać ponownie.";
                ShowAuthResult = true;
                AuthResultMessage = BiometricAuthFeedback.FormatFailure(auth);
                return;
            }

            VoiceCaptureHelper.Complete(new VoiceCaptureResult(samples, qualities.Average()));
            await _navigation.GoBackAsync();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Anulowano.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd nagrywania: {ex.Message}";
        }
        finally
        {
            IsRecording = false;
            CountdownText = string.Empty;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        VoiceCaptureHelper.Cancel();
        await _navigation.GoBackAsync();
    }
}
