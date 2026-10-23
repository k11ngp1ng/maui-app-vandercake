using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Views;
public partial class RegistrationPage : ContentPage { public RegistrationPage(RegistrationViewModel vm) { InitializeComponent(); PageLifecycle.Connect(this, vm); } }
