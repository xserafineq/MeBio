using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeBio.Models;
using MeBio.Services;

namespace MeBio.ViewModels;

[QueryProperty(nameof(UserId), "userId")]
public partial class UserEditViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private int _userId;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _ageText = string.Empty;

    [ObservableProperty]
    private Gender _gender = Gender.Male;

    [ObservableProperty]
    private UserRole _role = UserRole.User;

    [ObservableProperty]
    private bool _isVerified;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsNew => UserId == 0;
    public IList<Gender> Genders { get; } = [Gender.Male, Gender.Female, Gender.Other];

    public UserEditViewModel(IUserService userService, INavigationService navigation)
    {
        _userService = userService;
        _navigation = navigation;
    }

    partial void OnUserIdChanged(int value) => _ = LoadUserAsync();

    private async Task LoadUserAsync()
    {
        if (UserId == 0)
        {
            FirstName = string.Empty;
            LastName = string.Empty;
            Email = string.Empty;
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            AgeText = string.Empty;
            Gender = Gender.Male;
            Role = UserRole.User;
            IsVerified = false;
            return;
        }

        var user = await _userService.GetByIdAsync(UserId);
        if (user is null) return;

        FirstName = user.FirstName;
        LastName = user.LastName;
        Email = user.Email;
        AgeText = user.Age.ToString();
        Gender = user.Gender;
        Role = user.Role;
        IsVerified = user.IsVerified;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            if (!int.TryParse(AgeText, out var age))
            {
                StatusMessage = "Podaj prawidłowy wiek.";
                return;
            }

            if (IsNew)
            {
                if (string.IsNullOrWhiteSpace(Password))
                {
                    StatusMessage = "Hasło jest wymagane.";
                    return;
                }

                if (Password != ConfirmPassword)
                {
                    StatusMessage = "Hasła nie są identyczne.";
                    return;
                }

                var (success, message) = await _userService.CreateAsync(
                    FirstName, LastName, Email, Password, age, Gender, Role);
                StatusMessage = message;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(Password) || !string.IsNullOrWhiteSpace(ConfirmPassword))
                {
                    if (Password != ConfirmPassword)
                    {
                        StatusMessage = "Hasła nie są identyczne.";
                        return;
                    }
                }

                var (success, message) = await _userService.UpdateAsync(
                    UserId, FirstName, LastName, Email, age, Gender, Role, IsVerified, Password, ConfirmPassword);
                StatusMessage = message;
            }

            if (StatusMessage is "Użytkownik dodany." or "Zapisano.")
                await _navigation.GoBackAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync() => await _navigation.GoBackAsync();
}
