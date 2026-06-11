namespace MeBio.Helpers;

public static class ValidationHelper
{
    public static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Contains('.');

    public static (bool Valid, string Message) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Hasło jest wymagane.");
        if (password.Length < 4)
            return (false, "Hasło musi mieć min. 4 znaki.");
        return (true, string.Empty);
    }

    public static (bool Valid, string Message) ValidateAge(int age)
    {
        if (age < 1 || age > 150)
            return (false, "Podaj prawidłowy wiek (1–150).");
        return (true, string.Empty);
    }
}
