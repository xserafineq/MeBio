using MeBio.Models;
using MeBio.ViewModels;

namespace MeBio.Views;

public partial class UserEditPage : ContentPage
{
    public UserEditPage(UserEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        RolePicker.ItemsSource = Enum.GetValues<UserRole>();
    }
}
