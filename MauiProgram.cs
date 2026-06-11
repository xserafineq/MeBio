using CommunityToolkit.Maui;
using MeBio.Data;
using MeBio.Services;
using MeBio.ViewModels;
using MeBio.Views;
using Microsoft.Extensions.Logging;

namespace MeBio;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitCamera()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<AppDbContext>();
        builder.Services.AddSingleton<PasswordHasher>();
        builder.Services.AddSingleton<IFaceRecognitionService, LandmarkFaceRecognitionService>();
        builder.Services.AddSingleton<IFingerprintRecognitionService, SourceAfisFingerprintRecognitionService>();
        builder.Services.AddSingleton<ICameraAvailabilityService, CameraAvailabilityService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IUserService, UserService>();
        builder.Services.AddSingleton<IBiometricStatsService, BiometricStatsService>();

        builder.Services.AddTransient<AppShell>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<FaceCaptureViewModel>();
        builder.Services.AddTransient<FingerprintCaptureViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddTransient<UserEditViewModel>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<FaceCapturePage>();
        builder.Services.AddTransient<FingerprintCapturePage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<UserEditPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
