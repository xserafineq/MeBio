using System.Globalization;
using MeBio.Helpers;
using MeBio.Models;

namespace MeBio.Converters;

public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrWhiteSpace(s);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class LoginMethodConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LoginMethod method)
            return LoginMethodLabels.ToDisplayName(method);

        return value?.ToString() switch
        {
            "Password" => "Hasło",
            "Face" => "Twarz",
            "Fingerprint" => "Odcisk palca",
            "OtherBiometric" => "Inna biometria",
            _ => value?.ToString() ?? "—"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class LoginMethodColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LoginMethod method)
            return LoginMethodLabels.ToBadgeColor(method);

        if (Enum.TryParse<LoginMethod>(value?.ToString(), out var parsed))
            return LoginMethodLabels.ToBadgeColor(parsed);

        return Color.FromArgb("#8899AA");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class SuccessColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Color.FromArgb("#00E676") : Color.FromArgb("#FF5252");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class GenderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Gender gender)
            return GenderLabels.ToDisplayName(gender);
        return value?.ToString() ?? "—";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class BoolToResultTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Sukces" : "Błąd";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
