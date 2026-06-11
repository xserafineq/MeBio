using MeBio.Models;

namespace MeBio.Helpers;

public static class LoginMethodLabels
{
    public static string ToDisplayName(LoginMethod method) => method switch
    {
        LoginMethod.Password => "Hasło",
        LoginMethod.Face => "Twarz",
        LoginMethod.Fingerprint => "Odcisk palca",
        LoginMethod.OtherBiometric => "Inna biometria",
        LoginMethod.Voice => "Głos",
        _ => method.ToString()
    };

    public static Color ToBadgeColor(LoginMethod method) => method switch
    {
        LoginMethod.Password => Color.FromArgb("#4FC3F7"),
        LoginMethod.Face => Color.FromArgb("#00E676"),
        LoginMethod.Fingerprint => Color.FromArgb("#FFB74D"),
        LoginMethod.OtherBiometric => Color.FromArgb("#CE93D8"),
        LoginMethod.Voice => Color.FromArgb("#FF8A65"),
        _ => Color.FromArgb("#8899AA")
    };
}
