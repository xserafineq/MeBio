using MeBio.Data;
using MeBio.Models;
using Microsoft.EntityFrameworkCore;

namespace MeBio.Services;

public record DailyLoginStat(DateTime Date, int SuccessCount, int FailCount);

public record MethodEffectivenessStat(LoginMethod Method, int TotalAttempts, int SuccessCount)
{
    public double SuccessRate => TotalAttempts > 0 ? (double)SuccessCount / TotalAttempts * 100 : 0;
}

public record BiometricOverview(
    int TotalUsers,
    int VerifiedUsers,
    int TotalLogins,
    int FaceLogins,
    int PasswordLogins,
    double SuccessRate,
    List<DailyLoginStat> Last7Days,
    List<MethodEffectivenessStat> MethodStats,
    List<LoginAttempt> RecentAttempts);

public record UserBiometricOverview(
    int TotalLogins,
    int FaceLogins,
    int PasswordLogins,
    int VoiceLogins,
    double SuccessRate,
    List<DailyLoginStat> Last7Days,
    List<MethodEffectivenessStat> MethodStats,
    List<LoginAttempt> RecentAttempts,
    bool HasFaceTemplate,
    double? FaceQualityScore,
    DateTime? FaceCapturedAt,
    byte[]? FacePreviewImage,
    bool HasVoiceTemplate,
    double? VoiceQualityScore,
    DateTime? VoiceCapturedAt,
    string? VoiceAudioPath);

public interface IBiometricStatsService
{
    Task<BiometricOverview> GetAdminOverviewAsync();
    Task<UserBiometricOverview> GetUserOverviewAsync(int userId);
}

public class BiometricStatsService : IBiometricStatsService
{
    private readonly AppDbContext _db;

    public BiometricStatsService(AppDbContext db) => _db = db;

    public async Task<BiometricOverview> GetAdminOverviewAsync()
    {
        var users = await _db.Users.CountAsync();
        var verified = await _db.Users.CountAsync(u => u.IsVerified);
        var attempts = await _db.LoginAttempts.ToListAsync();

        var recent = await _db.LoginAttempts
            .OrderByDescending(a => a.AttemptedAt)
            .Take(15)
            .ToListAsync();

        return BuildOverview(attempts, users, verified, recent);
    }

    public async Task<UserBiometricOverview> GetUserOverviewAsync(int userId)
    {
        var attempts = await _db.LoginAttempts
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.AttemptedAt)
            .ToListAsync();

        var face = await _db.FaceTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == userId);

        var voice = await _db.VoiceTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UserId == userId);

        var total = attempts.Count;
        var success = attempts.Count(a => a.Success);

        var today = DateTime.UtcNow.Date;
        var last7 = BuildLast7Days(attempts, today);

        return new UserBiometricOverview(
            total,
            attempts.Count(a => a.Method == LoginMethod.Face),
            attempts.Count(a => a.Method == LoginMethod.Password),
            attempts.Count(a => a.Method == LoginMethod.Voice),
            total > 0 ? (double)success / total * 100 : 0,
            last7,
            BuildMethodStats(attempts),
            attempts.Take(15).ToList(),
            face is not null,
            face?.QualityScore,
            face?.CapturedAt,
            face?.PreviewImage,
            voice is not null,
            voice?.QualityScore,
            voice?.CapturedAt,
            voice?.AudioFilePath);
    }

    private static BiometricOverview BuildOverview(
        List<LoginAttempt> attempts, int users, int verified, List<LoginAttempt> recent)
    {
        var total = attempts.Count;
        var success = attempts.Count(a => a.Success);
        var today = DateTime.UtcNow.Date;
        var last7 = BuildLast7Days(attempts, today);

        return new BiometricOverview(
            users,
            verified,
            total,
            attempts.Count(a => a.Method == LoginMethod.Face),
            attempts.Count(a => a.Method == LoginMethod.Password),
            total > 0 ? (double)success / total * 100 : 0,
            last7,
            BuildMethodStats(attempts),
            recent);
    }

    private static List<MethodEffectivenessStat> BuildMethodStats(List<LoginAttempt> attempts) =>
        Enum.GetValues<LoginMethod>()
            .Select(method =>
            {
                var methodAttempts = attempts.Where(a => a.Method == method).ToList();
                return new MethodEffectivenessStat(
                    method,
                    methodAttempts.Count,
                    methodAttempts.Count(a => a.Success));
            })
            .Where(s => s.TotalAttempts > 0)
            .OrderByDescending(s => s.TotalAttempts)
            .ToList();

    private static List<DailyLoginStat> BuildLast7Days(List<LoginAttempt> attempts, DateTime today) =>
        Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .Select(date =>
            {
                var dayAttempts = attempts.Where(a => a.AttemptedAt.Date == date).ToList();
                return new DailyLoginStat(
                    date,
                    dayAttempts.Count(a => a.Success),
                    dayAttempts.Count(a => !a.Success));
            })
            .ToList();
}
