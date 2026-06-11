using MeBio.Data;
using MeBio.Helpers;
using MeBio.Models;

namespace MeBio.Services;

internal static class VoiceTemplatePersistence
{
    public static async Task SaveAsync(
        AppDbContext db,
        User user,
        VoiceEnrollmentResult enrollment,
        IReadOnlyList<byte[]> wavSamples)
    {
        var templateBytes = AudioSignalHelper.PackEmbeddings(enrollment.Embeddings);
        var audioPath = await VoiceFileStorage.SaveAsync(user.Id, wavSamples[0]);

        if (user.VoiceTemplate is not null)
        {
            user.VoiceTemplate.TemplateData = templateBytes;
            user.VoiceTemplate.AudioFilePath = audioPath;
            user.VoiceTemplate.QualityScore = enrollment.AverageQuality;
            user.VoiceTemplate.MatchThreshold = enrollment.MatchThreshold;
            user.VoiceTemplate.Algorithm = "MfccV3";
            user.VoiceTemplate.CapturedAt = DateTime.UtcNow;
        }
        else
        {
            db.VoiceTemplates.Add(new VoiceTemplate
            {
                UserId = user.Id,
                TemplateData = templateBytes,
                AudioFilePath = audioPath,
                QualityScore = enrollment.AverageQuality,
                MatchThreshold = enrollment.MatchThreshold,
                Algorithm = "MfccV3"
            });
        }

        user.IsVerified = true;
    }
}
