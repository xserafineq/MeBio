namespace MeBio.Models;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public int Age { get; set; }
    public Gender Gender { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public FaceTemplate? FaceTemplate { get; set; }
    public VoiceTemplate? VoiceTemplate { get; set; }
    public ICollection<LoginAttempt> LoginAttempts { get; set; } = [];
}
