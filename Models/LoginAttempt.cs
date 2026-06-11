namespace MeBio.Models;

public class LoginAttempt
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public bool Success { get; set; }
    public LoginMethod Method { get; set; }
    public double? MatchScore { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
