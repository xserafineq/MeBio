using MeBio.Data;
using MeBio.Helpers;
using MeBio.Models;
using Microsoft.EntityFrameworkCore;

namespace MeBio.Services;

public interface IAuthService
{
    Task<(bool Success, string Message, User? User)> LoginWithPasswordAsync(string email, string password);
    Task<(bool Success, string Message, User? User)> LoginWithFaceAsync(byte[] imageBytes, string email);
    Task<(bool Success, string Message, User? User)> LoginWithVoiceAsync(byte[] wavBytes, string email);
    Task<(bool Success, string Message)> RegisterAsync(
        string firstName, string lastName, string email, string password, int age, Gender gender,
        byte[]? faceImage = null, byte[]? voiceAudio = null);
    Task LogoutAsync();
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly IFaceRecognitionService _faceService;
    private readonly IVoiceRecognitionService _voiceService;
    private readonly ISessionService _session;

    public AuthService(
        AppDbContext db,
        PasswordHasher hasher,
        IFaceRecognitionService faceService,
        IVoiceRecognitionService voiceService,
        ISessionService session)
    {
        _db = db;
        _hasher = hasher;
        _faceService = faceService;
        _voiceService = voiceService;
        _session = session;
    }

    public async Task<(bool Success, string Message, User? User)> LoginWithPasswordAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            await LogAttemptAsync(null, email, false, LoginMethod.Password);
            return (false, "Nieprawidłowy email lub hasło.", null);
        }

        if (!_hasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Password);
            return (false, "Nieprawidłowy email lub hasło.", null);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogAttemptAsync(user.Id, email, true, LoginMethod.Password);
        _session.SetUser(user);
        return (true, "Zalogowano.", user);
    }

    public async Task<(bool Success, string Message, User? User)> LoginWithFaceAsync(byte[] imageBytes, string email)
    {
        email = email.Trim().ToLowerInvariant();
        var liveTemplate = _faceService.ExtractTemplate(imageBytes);
        var templates = await _db.FaceTemplates
            .Include(f => f.User)
            .Where(f => f.User.IsVerified)
            .ToListAsync();

        User? bestUser = null;
        var bestScore = 0.0;

        foreach (var stored in templates)
        {
            var match = _faceService.Verify(liveTemplate, stored.TemplateData);
            if (match.Score > bestScore)
            {
                bestScore = match.Score;
                bestUser = stored.User;
            }
        }

        if (bestUser is null || bestScore < FaceRecognitionDefaults.MatchThreshold)
        {
            await LogAttemptAsync(null, email, false, LoginMethod.Face, bestScore);
            return (false, "Nie rozpoznano twarzy.", null);
        }

        if (!string.Equals(bestUser.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            await LogAttemptAsync(bestUser.Id, email, false, LoginMethod.Face, bestScore);
            return (false, "Twarz nie pasuje do podanego konta.", null);
        }

        bestUser.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogAttemptAsync(bestUser.Id, email, true, LoginMethod.Face, bestScore);
        _session.SetUser(bestUser);
        return (true, "Zalogowano twarzą.", bestUser);
    }

    public async Task<(bool Success, string Message, User? User)> LoginWithVoiceAsync(byte[] wavBytes, string email)
    {
        email = email.Trim().ToLowerInvariant();
        var liveTemplate = _voiceService.ExtractTemplate(wavBytes);
        var templates = await _db.VoiceTemplates
            .Include(v => v.User)
            .Where(v => v.User.IsVerified)
            .ToListAsync();

        User? bestUser = null;
        var bestScore = 0.0;

        foreach (var stored in templates)
        {
            var match = _voiceService.Verify(liveTemplate, stored.TemplateData);
            if (match.Score > bestScore)
            {
                bestScore = match.Score;
                bestUser = stored.User;
            }
        }

        if (bestUser is null || bestScore < VoiceRecognitionDefaults.MatchThreshold)
        {
            await LogAttemptAsync(null, email, false, LoginMethod.Voice, bestScore);
            return (false, "Nie rozpoznano głosu.", null);
        }

        if (!string.Equals(bestUser.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            await LogAttemptAsync(bestUser.Id, email, false, LoginMethod.Voice, bestScore);
            return (false, "Głos nie pasuje do podanego konta.", null);
        }

        bestUser.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogAttemptAsync(bestUser.Id, email, true, LoginMethod.Voice, bestScore);
        _session.SetUser(bestUser);
        return (true, "Zalogowano głosem.", bestUser);
    }

    public async Task<(bool Success, string Message)> RegisterAsync(
        string firstName, string lastName, string email, string password, int age, Gender gender,
        byte[]? faceImage = null, byte[]? voiceAudio = null)
    {
        firstName = firstName.Trim();
        lastName = lastName.Trim();
        email = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return (false, "Imię i nazwisko są wymagane.");

        if (!ValidationHelper.IsValidEmail(email))
            return (false, "Podaj prawidłowy adres email.");

        var pwdCheck = ValidationHelper.ValidatePassword(password);
        if (!pwdCheck.Valid) return (false, pwdCheck.Message);

        var ageCheck = ValidationHelper.ValidateAge(age);
        if (!ageCheck.Valid) return (false, ageCheck.Message);

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return (false, "Użytkownik z tym emailem już istnieje.");

        var (hash, salt) = _hasher.Hash(password);
        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Age = age,
            Gender = gender,
            Role = UserRole.User,
            IsVerified = faceImage is not null || voiceAudio is not null,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        if (faceImage is not null)
        {
            var quality = _faceService.ComputeQualityScore(faceImage);
            if (quality < 30)
                return (false, "Jakość zdjęcia twarzy jest zbyt niska. Spróbuj ponownie.");

            var template = _faceService.ExtractTemplate(faceImage);
            _db.FaceTemplates.Add(new FaceTemplate
            {
                UserId = user.Id,
                TemplateData = template,
                PreviewImage = faceImage,
                QualityScore = quality,
                Algorithm = "SimpleV1"
            });
            await _db.SaveChangesAsync();
        }

        if (voiceAudio is not null)
        {
            var quality = _voiceService.ComputeQualityScore(voiceAudio);
            if (quality < VoiceRecognitionDefaults.MinQualityScore)
                return (false, "Jakość nagrania głosu jest zbyt niska. Spróbuj ponownie.");

            var template = _voiceService.ExtractTemplate(voiceAudio);
            var audioPath = await VoiceFileStorage.SaveAsync(user.Id, voiceAudio);
            _db.VoiceTemplates.Add(new VoiceTemplate
            {
                UserId = user.Id,
                TemplateData = template,
                AudioFilePath = audioPath,
                QualityScore = quality,
                Algorithm = "SimpleV1"
            });
            user.IsVerified = true;
            await _db.SaveChangesAsync();
        }

        return (true, "Konto utworzone.");
    }

    public Task LogoutAsync()
    {
        _session.Clear();
        return Task.CompletedTask;
    }

    private async Task LogAttemptAsync(int? userId, string? email, bool success, LoginMethod method, double? score = null)
    {
        _db.LoginAttempts.Add(new LoginAttempt
        {
            UserId = userId,
            UserEmail = email,
            Success = success,
            Method = method,
            MatchScore = score,
            AttemptedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
