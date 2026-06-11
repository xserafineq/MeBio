using MeBio.Data;
using MeBio.Helpers;
using MeBio.Models;
using Microsoft.EntityFrameworkCore;

namespace MeBio.Services;

public interface IUserService
{
    Task<List<User>> GetAllAsync();
    Task<User?> GetByIdAsync(int id);
    Task<(bool Success, string Message)> CreateAsync(
        string firstName, string lastName, string email, string password, int age, Gender gender, UserRole role);
    Task<(bool Success, string Message)> UpdateAsync(
        int id, string firstName, string lastName, string email, int age, Gender gender, UserRole role, bool isVerified,
        string? newPassword = null, string? confirmPassword = null);
    Task<(bool Success, string Message)> UpdateProfileAsync(int userId, string email, string? newPassword, string? confirmPassword);
    Task<(bool Success, string Message)> DeleteAsync(int id);
    Task<(bool Success, string Message)> SetFaceTemplateAsync(int userId, byte[] imageBytes);
    Task<(bool Success, string Message)> SetFingerprintTemplateAsync(int userId, byte[] imageBytes);
}

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly IFaceRecognitionService _faceService;
    private readonly IFingerprintRecognitionService _fingerprintService;
    private readonly ISessionService _session;

    public UserService(
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

    public Task<List<User>> GetAllAsync() =>
        _db.Users.Include(u => u.FaceTemplate).Include(u => u.FingerprintTemplate)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync();

    public Task<User?> GetByIdAsync(int id) =>
        _db.Users.Include(u => u.FaceTemplate).Include(u => u.FingerprintTemplate)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<(bool Success, string Message)> CreateAsync(
        string firstName, string lastName, string email, string password, int age, Gender gender, UserRole role)
    {
        firstName = firstName.Trim();
        lastName = lastName.Trim();
        email = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return (false, "Imię i nazwisko są wymagane.");

        if (!ValidationHelper.IsValidEmail(email))
            return (false, "Podaj prawidłowy email.");

        var pwdCheck = ValidationHelper.ValidatePassword(password);
        if (!pwdCheck.Valid) return (false, pwdCheck.Message);

        var ageCheck = ValidationHelper.ValidateAge(age);
        if (!ageCheck.Valid) return (false, ageCheck.Message);

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return (false, "Email zajęty.");

        var (hash, salt) = _hasher.Hash(password);
        _db.Users.Add(new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Age = age,
            Gender = gender,
            Role = role,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return (true, "Użytkownik dodany.");
    }

    public async Task<(bool Success, string Message)> UpdateAsync(
        int id, string firstName, string lastName, string email, int age, Gender gender, UserRole role, bool isVerified,
        string? newPassword = null, string? confirmPassword = null)
    {
        var user = await _db.Users
            .Include(u => u.FaceTemplate)
            .Include(u => u.FingerprintTemplate)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return (false, "Nie znaleziono użytkownika.");

        firstName = firstName.Trim();
        lastName = lastName.Trim();
        email = email.Trim().ToLowerInvariant();

        if (!ValidationHelper.IsValidEmail(email))
            return (false, "Podaj prawidłowy email.");

        var ageCheck = ValidationHelper.ValidateAge(age);
        if (!ageCheck.Valid) return (false, ageCheck.Message);

        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != id))
            return (false, "Email zajęty.");

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = email;
        user.Age = age;
        user.Gender = gender;
        user.Role = role;
        user.IsVerified = isVerified || user.FaceTemplate is not null || user.FingerprintTemplate is not null;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword != confirmPassword)
                return (false, "Hasła nie są identyczne.");

            var pwdCheck = ValidationHelper.ValidatePassword(newPassword);
            if (!pwdCheck.Valid) return (false, pwdCheck.Message);

            var (hash, salt) = _hasher.Hash(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
        }

        await _db.SaveChangesAsync();

        if (_session.CurrentUser?.Id == id)
            _session.SetUser(user);

        return (true, "Zapisano.");
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(
        int userId, string email, string? newPassword, string? confirmPassword)
    {
        if (_session.CurrentUser?.Id != userId)
            return (false, "Możesz edytować tylko własny profil.");

        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return (false, "Nie znaleziono użytkownika.");

        email = email.Trim().ToLowerInvariant();
        if (!ValidationHelper.IsValidEmail(email))
            return (false, "Podaj prawidłowy email.");

        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != userId))
            return (false, "Ten email jest już zajęty.");

        user.Email = email;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword != confirmPassword)
                return (false, "Hasła nie są identyczne.");

            var pwdCheck = ValidationHelper.ValidatePassword(newPassword);
            if (!pwdCheck.Valid) return (false, pwdCheck.Message);

            var (hash, salt) = _hasher.Hash(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
        }

        await _db.SaveChangesAsync();
        _session.SetUser(user);
        return (true, "Profil zaktualizowany.");
    }

    public async Task<(bool Success, string Message)> DeleteAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
            return (false, "Nie znaleziono użytkownika.");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return (true, "Usunięto.");
    }

    public async Task<(bool Success, string Message)> SetFaceTemplateAsync(int userId, byte[] imageBytes)
    {
        if (_session.CurrentUser?.Id != userId)
            return (false, "Biometrię może zmienić tylko właściciel konta.");

        var user = await _db.Users.Include(u => u.FaceTemplate).FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return (false, "Nie znaleziono użytkownika.");

        var quality = _faceService.ComputeQualityScore(imageBytes);
        if (quality < FaceRecognitionDefaults.MinQualityScore)
            return (false, quality < 1
                ? "Nie wykryto twarzy na zdjęciu."
                : "Jakość zdjęcia zbyt niska.");

        byte[] template;
        try
        {
            template = _faceService.ExtractTemplate(imageBytes);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }

        if (user.FaceTemplate is not null)
        {
            user.FaceTemplate.TemplateData = template;
            user.FaceTemplate.PreviewImage = imageBytes;
            user.FaceTemplate.QualityScore = quality;
            user.FaceTemplate.MatchThreshold = FaceRecognitionDefaults.DefaultMatchThreshold;
            user.FaceTemplate.Algorithm = FaceRecognitionDefaults.AlgorithmVersion;
            user.FaceTemplate.CapturedAt = DateTime.UtcNow;
        }
        else
        {
            _db.FaceTemplates.Add(new FaceTemplate
            {
                UserId = userId,
                TemplateData = template,
                PreviewImage = imageBytes,
                QualityScore = quality,
                MatchThreshold = FaceRecognitionDefaults.DefaultMatchThreshold,
                Algorithm = FaceRecognitionDefaults.AlgorithmVersion
            });
        }

        user.IsVerified = true;
        await _db.SaveChangesAsync();
        _session.SetUser(user);
        return (true, "Biometria zapisana.");
    }

    public async Task<(bool Success, string Message)> SetFingerprintTemplateAsync(int userId, byte[] imageBytes)
    {
        if (_session.CurrentUser?.Id != userId)
            return (false, "Biometrię może zmienić tylko właściciel konta.");

        var user = await _db.Users.Include(u => u.FingerprintTemplate).FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return (false, "Nie znaleziono użytkownika.");

        var quality = _fingerprintService.ComputeQualityScore(imageBytes);
        if (quality < FingerprintRecognitionDefaults.MinQualityScore)
            return (false, quality < 1
                ? "Nie wykryto odcisku palca na zdjęciu."
                : "Jakość zdjęcia zbyt niska.");

        byte[] template;
        try
        {
            template = _fingerprintService.ExtractTemplate(imageBytes);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }

        if (user.FingerprintTemplate is not null)
        {
            user.FingerprintTemplate.TemplateData = template;
            user.FingerprintTemplate.PreviewImage = imageBytes;
            user.FingerprintTemplate.QualityScore = quality;
            user.FingerprintTemplate.MatchThreshold = FingerprintRecognitionDefaults.DefaultMatchThreshold;
            user.FingerprintTemplate.Algorithm = FingerprintRecognitionDefaults.AlgorithmVersion;
            user.FingerprintTemplate.CapturedAt = DateTime.UtcNow;
        }
        else
        {
            _db.FingerprintTemplates.Add(new FingerprintTemplate
            {
                UserId = userId,
                TemplateData = template,
                PreviewImage = imageBytes,
                QualityScore = quality,
                MatchThreshold = FingerprintRecognitionDefaults.DefaultMatchThreshold,
                Algorithm = FingerprintRecognitionDefaults.AlgorithmVersion
            });
        }

        user.IsVerified = true;
        await _db.SaveChangesAsync();
        _session.SetUser(user);
        return (true, "Odcisk palca zapisany.");
    }
}
