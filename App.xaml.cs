using MeBio.Data;
using MeBio.Services;
using MeBio.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MeBio;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash(e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash(e.Exception);
            e.SetObserved();
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var loginPage = _services.GetRequiredService<LoginPage>();
        NavigationPage.SetHasNavigationBar(loginPage, false);

        var window = new Window(new NavigationPage(loginPage));
        _ = InitDbAsync();
        return window;
    }

    private async Task InitDbAsync()
    {
        try
        {
            var db = _services.GetRequiredService<AppDbContext>();
            var hasher = _services.GetRequiredService<PasswordHasher>();
            await DatabaseInitializer.InitializeAsync(db, hasher);
        }
        catch (Exception ex)
        {
            LogCrash(ex);
        }
    }

    private static void LogCrash(Exception? ex)
    {
        if (ex is null) return;
        System.Diagnostics.Debug.WriteLine(ex);
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Current?.Windows.FirstOrDefault()?.Page is Page page)
                await page.DisplayAlertAsync("Błąd", ex.Message, "OK");
        });
    }
}
