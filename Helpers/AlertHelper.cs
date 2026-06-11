namespace MeBio.Helpers;

public static class AlertHelper
{
    public static async Task ShowAsync(string title, string message)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
            await page.DisplayAlertAsync(title, message, "OK");
    }
}
