using MeBio.Data;
using MeBio.Helpers;
using MeBio.Models;
using Microsoft.EntityFrameworkCore;

namespace MeBio.Services;

public interface IAuthService
{
    Task<(bool Success, string Message, User? User)> LoginWithPasswordAsync(string email, string password);
    Task<BiometricLoginResult> LoginWithFaceAsync(byte[] imageBytes, string email);
    Task<BiometricLoginResult> LoginWithFingerprintAsync(byte[] imageBytes, string email);
    Task<(bool Success, string Message)> RegisterAsync(
        string firstName, string lastName, string email, string password, int age, Gender gender,
        byte[]? faceImage = null, byte[]? fingerprintImage = null);
    Task LogoutAsync();
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly IFaceRecognitionService _faceService;
    private readonly IFingerprintRecognitionService _fingerprintService;
    private readonly ISessionService _session;

    public AuthService(
        AppDbContext db,
        PasswordHasher hasher,
        IFaceRecognitionService faceService,
        IFingerprintRecognitionService fingerprintService,
        ISessionService session)
    {
        _db = db;
        _hasher = hasher;
        _faceService = faceService;
        _fingerprintService = fingerprintService;
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
        _session.SetUser(SnapshotUser(user));
        return (true, "Zalogowano.", user);
    }

    public async Task<BiometricLoginResult> LoginWithFaceAsync(byte[] imageBytes, string email)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.FaceTemplate)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            await LogAttemptAsync(null, email, false, LoginMethod.Face);
            return new BiometricLoginResult(false, "Nie znaleziono konta z tym adresem email.", null);
        }

        if (user.FaceTemplate is null)
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Face);
            return new BiometricLoginResult(false, "To konto nie ma zarejestrowanej twarzy.", null);
        }

        if (user.FaceTemplate.Algorithm != FaceRecognitionDefaults.AlgorithmVersion)
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Face);
            return new BiometricLoginResult(
                false,
                "Profil twarzy wymaga ponownej rejestracji w profilu.",
                null);
        }

        var quality = _faceService.ComputeQualityScore(imageBytes);
        if (quality < FaceRecognitionDefaults.MinQualityScore)
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Face, quality / 100);
            return new BiometricLoginResult(
                false,
                quality < 1
                    ? "Nie wykryto twarzy — przybliż twarz do kamery i ustaw ją w środku kadru."
                    : "Zbyt niska jakość zdjęcia — popraw oświetlenie.",
                null,
                null,
                null,
                quality);
        }

        byte[] liveTemplate;
        try
        {
            liveTemplate = _faceService.ExtractTemplate(imageBytes);
        }
        catch (InvalidOperationException ex)
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Face);
            return new BiometricLoginResult(false, ex.Message, null, null, null, quality);
        }

        var threshold = Math.Clamp(
            user.FaceTemplate.MatchThreshold,
            FaceRecognitionDefaults.MinMatchThreshold,
            FaceRecognitionDefaults.MaxMatchThreshold);
        var match = _faceService.Verify(liveTemplate, user.FaceTemplate.TemplateData, threshold);

        if (!match.IsMatch)
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Face, match.Score);
            return new BiometricLoginResult(
                false,
                "Nie rozpoznano twarzy.",
                null,
                match.Score,
                threshold,
                quality);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogAttemptAsync(user.Id, email, true, LoginMethod.Face, match.Score);
        _session.SetUser(SnapshotUser(user));
        return new BiometricLoginResult(true, "Zalogowano twarzą.", user, match.Score, threshold, quality);
    }

    public async Task<BiometricLoginResult> LoginWithFingerprintAsync(byte[] imageBytes, string email)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.FingerprintTemplate)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            await LogAttemptAsync(null, email, false, LoginMethod.Fingerprint);
            return new BiometricLoginResult(false, "Nie znaleziono konta z tym adresem email.", null);
        }

        if (user.FingerprintTemplate is null)
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Fingerprint);
            return new BiometricLoginResult(false, "To konto nie ma zarejestrowanego odcisku palca.", null);
        }

        if (user.FingerprintTemplate.Algorithm != FingerprintRecognitionDefaults.AlgorithmVersion)
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Fingerprint);
            return new BiometricLoginResult(
                false,
                "Profil odcisku wymaga ponownej rejestracji w profilu.",
                null);
        }

        var quality = _fingerprintService.ComputeQualityScore(imageBytes);
        if (quality < FingerprintRecognitionDefaults.MinQualityScore)
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Fingerprint, quality / 100);
            return new BiometricLoginResult(
                false,
                quality < 1
                    ? "Nie wykryto odcisku palca — ustaw palec w środku kadru."
                    : "Zbyt niska jakość zdjęcia — popraw oświetlenie.",
                null,
                null,
                null,
                quality);
        }

        var threshold = FingerprintRecognitionDefaults.DefaultMatchThreshold;
        var match = _fingerprintService.Verify(imageBytes, user.FingerprintTemplate.TemplateData, threshold);

        if (!match.IsMatch)
        {
            await LogAttemptAsync(user.Id, email, false, LoginMethod.Fingerprint, match.Score);
            return new BiometricLoginResult(
                false,
                "Nie rozpoznano odcisku palca.",
                null,
                match.Score,
                threshold,
                quality);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogAttemptAsync(user.Id, email, true, LoginMethod.Fingerprint, match.Score);
        _session.SetUser(SnapshotUser(user));
        return new BiometricLoginResult(true, "Zalogowano odciskiem palca.", user, match.Score, threshold, quality);
    }

    public async Task<(bool Success, string Message)> RegisterAsync(
        string firstName, string lastName, string email, string password, int age, Gender gender,
        byte[]? faceImage = null, byte[]? fingerprintImage = null)
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
            IsVerified = faceImage is not null || fingerprintImage is not null,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        if (faceImage is not null)
        {
            var quality = _faceService.ComputeQualityScore(faceImage);
            if (quality < FaceRecognitionDefaults.MinQualityScore)
                return (false, quality < 1
                    ? "Nie wykryto twarzy na zdjęciu. Ustaw twarz w kadrze."
                    : "Jakość zdjęcia twarzy jest zbyt niska. Spróbuj ponownie.");

            byte[] template;
            try
            {
                template = _faceService.ExtractTemplate(faceImage);
            }
            catch (InvalidOperationException ex)
            {
                return (false, ex.Message);
            }

            _db.FaceTemplates.Add(new FaceTemplate
            {
                UserId = user.Id,
                TemplateData = template,
                PreviewImage = faceImage,
                QualityScore = quality,
                MatchThreshold = FaceRecognitionDefaults.DefaultMatchThreshold,
                Algorithm = FaceRecognitionDefaults.AlgorithmVersion
            });
            await _db.SaveChangesAsync();
        }

        if (fingerprintImage is not null)
        {
            var quality = _fingerprintService.ComputeQualityScore(fingerprintImage);
            if (quality < FingerprintRecognitionDefaults.MinQualityScore)
                return (false, quality < 1
                    ? "Nie wykryto odcisku palca na zdjęciu. Ustaw palec w kadrze."
                    : "Jakość zdjęcia odcisku jest zbyt niska. Spróbuj ponownie.");

            byte[] template;
            try
            {
                template = _fingerprintService.ExtractTemplate(fingerprintImage);
            }
            catch (InvalidOperationException ex)
            {
                return (false, ex.Message);
            }

            _db.FingerprintTemplates.Add(new FingerprintTemplate
            {
                UserId = user.Id,
                TemplateData = template,
                PreviewImage = fingerprintImage,
                QualityScore = quality,
                MatchThreshold = FingerprintRecognitionDefaults.DefaultMatchThreshold,
                Algorithm = FingerprintRecognitionDefaults.AlgorithmVersion
            });
            await _db.SaveChangesAsync();
        }

        return (true, "Konto utworzone.");
    }

    public Task LogoutAsync()
    {
        _session.Clear();
        return Task.CompletedTask;
    }

    private static User SnapshotUser(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Role = user.Role,
        IsVerified = user.IsVerified,
        Age = user.Age,
        Gender = user.Gender,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt
    };

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
