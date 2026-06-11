using MeBio.Models;
using MeBio.Services;
using MeBio.Views;

namespace MeBio;

public partial class AppShell : Shell
{
    public AppShell(DashboardPage dashboardPage, ProfilePage profilePage, UsersPage usersPage, ISessionService session)
    {
        InitializeComponent();

        var tabBar = new TabBar { Route = "main" };
        tabBar.Items.Add(new ShellContent
        {
            Title = "Dashboard",
            Content = dashboardPage,
            Route = "dashboard"
        });
        tabBar.Items.Add(new ShellContent
        {
            Title = "Profil",
            Content = profilePage,
            Route = "profile"
        });

        if (session.CurrentUser?.Role == UserRole.Admin)
        {
            tabBar.Items.Add(new ShellContent
            {
                Title = "Użytkownicy",
                Content = usersPage,
                Route = "users"
            });
        }

        Items.Add(tabBar);

        Routing.RegisterRoute(nameof(UserEditPage), typeof(UserEditPage));
        Routing.RegisterRoute(nameof(FaceCapturePage), typeof(FaceCapturePage));
    }
}
