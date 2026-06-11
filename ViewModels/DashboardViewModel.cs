using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Controls;
using MeBio.Models;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IBiometricStatsService _statsService;
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string _welcomeText = string.Empty;

    [ObservableProperty]
    private int _totalUsers;

    [ObservableProperty]
    private int _verifiedUsers;

    [ObservableProperty]
    private int _totalLogins;

    [ObservableProperty]
    private int _faceLogins;

    [ObservableProperty]
    private int _passwordLogins;

    [ObservableProperty]
    private double _successRate;

    [ObservableProperty]
    private List<DailyLoginStatProxy> _chartStats = [];

    [ObservableProperty]
    private List<MethodEffectivenessProxy> _methodStats = [];

    [ObservableProperty]
    private List<LoginAttempt> _recentAttempts = [];

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public DashboardViewModel(
        IBiometricStatsService statsService,
        ISessionService session,
        IAuthService authService,
        INavigationService navigation)
    {
        _statsService = statsService;
        _session = session;
        _authService = authService;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var user = _session.CurrentUser;
            WelcomeText = $"Witaj, {user?.FirstName ?? "użytkowniku"}";
            IsAdmin = user?.Role == UserRole.Admin;

            if (IsAdmin)
                await LoadAdminAsync();
            else if (user is not null)
                await LoadUserAsync(user.Id);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        await _navigation.GoToLoginAsync();
    }

    private async Task LoadAdminAsync()
    {
        var overview = await _statsService.GetAdminOverviewAsync();

        TotalUsers = overview.TotalUsers;
        VerifiedUsers = overview.VerifiedUsers;
        TotalLogins = overview.TotalLogins;
        FaceLogins = overview.FaceLogins;
        PasswordLogins = overview.PasswordLogins;
        SuccessRate = overview.SuccessRate;
        ChartStats = ToChartStats(overview.Last7Days);
        MethodStats = MethodEffectivenessMapper.ToProxies(overview.MethodStats);
        RecentAttempts = overview.RecentAttempts;
    }

    private async Task LoadUserAsync(int userId)
    {
        var overview = await _statsService.GetUserOverviewAsync(userId);

        TotalLogins = overview.TotalLogins;
        FaceLogins = overview.FaceLogins;
        PasswordLogins = overview.PasswordLogins;
        SuccessRate = overview.SuccessRate;
        ChartStats = ToChartStats(overview.Last7Days);
        MethodStats = MethodEffectivenessMapper.ToProxies(overview.MethodStats);
        RecentAttempts = overview.RecentAttempts;
    }

    private static List<DailyLoginStatProxy> ToChartStats(List<DailyLoginStat> days) =>
        days.Select(d => new DailyLoginStatProxy
        {
            Date = d.Date,
            SuccessCount = d.SuccessCount,
            FailCount = d.FailCount
        }).ToList();
}
