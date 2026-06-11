using MeBio.Models;

namespace MeBio.Helpers;

public static class GenderLabels
{
    public static string ToDisplayName(Gender gender) => gender switch
    {
        Gender.Male => "Mężczyzna",
        Gender.Female => "Kobieta",
        Gender.Other => "Inna",
        _ => gender.ToString()
    };
}
