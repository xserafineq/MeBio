using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Helpers;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class VoiceCaptureViewModel : ObservableObject
{
    private readonly IVoiceRecognitionService _voiceService;
    private readonly IAudioRecordingService _audioRecording;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _statusMessage = "Przygotuj się do nagrania.";

    [ObservableProperty]
    private string _countdownText = string.Empty;

    [ObservableProperty]
    private double _qualityScore;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRecording;

    public string Phrase => VoiceRecognitionDefaults.Phrase;

    public VoiceCaptureViewModel(
        IVoiceRecognitionService voiceService,
        IAudioRecordingService audioRecording,
        INavigationService navigation)
    {
        _voiceService = voiceService;
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
        StatusMessage = "Nagrywanie…";
        CountdownText = string.Empty;

        try
        {
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

            QualityScore = _voiceService.ComputeQualityScore(wavBytes);
            StatusMessage = $"Jakość: {QualityScore:F0}%";

            if (QualityScore < VoiceRecognitionDefaults.MinQualityScore)
            {
                StatusMessage = "Zbyt niska jakość — mów głośniej i bliżej mikrofonu.";
                return;
            }

            var template = _voiceService.ExtractTemplate(wavBytes);
            VoiceCaptureHelper.Complete(new VoiceCaptureResult(wavBytes, template, QualityScore));
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
