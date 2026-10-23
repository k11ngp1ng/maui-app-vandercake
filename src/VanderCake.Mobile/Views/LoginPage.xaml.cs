using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Views;
public partial class LoginPage : ContentPage { public LoginPage(LoginViewModel vm) { InitializeComponent(); PageLifecycle.Connect(this, vm); } }
