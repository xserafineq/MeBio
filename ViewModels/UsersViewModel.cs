using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Models;
using MeBio.Services;

namespace MeBio.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private List<User> _users = [];

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAdmin;

    public UsersViewModel(IUserService userService, ISessionService session, INavigationService navigation)
    {
        _userService = userService;
        _session = session;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            IsAdmin = _session.CurrentUser?.Role == UserRole.Admin;
            Users = await _userService.GetAllAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddUserAsync()
    {
        if (!IsAdmin) return;
        await _navigation.GoToUserEditAsync(0);
    }

    [RelayCommand]
    private async Task EditUserAsync(User user)
    {
        if (!IsAdmin || user is null) return;
        await _navigation.GoToUserEditAsync(user.Id);
    }

    [RelayCommand]
    private async Task DeleteUserAsync(User user)
    {
        if (!IsAdmin || user is null) return;

        if (user.Id == _session.CurrentUser?.Id)
        {
            StatusMessage = "Nie możesz usunąć własnego konta.";
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Usuń", $"Usunąć użytkownika {user.FullName}?", "Tak", "Nie");
        if (!confirm) return;

        var (success, message) = await _userService.DeleteAsync(user.Id);
        StatusMessage = message;
        if (success)
            await LoadAsync();
    }
}
