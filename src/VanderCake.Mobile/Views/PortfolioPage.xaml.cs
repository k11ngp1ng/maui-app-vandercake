using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Views;
public partial class PortfolioPage : ContentPage { public PortfolioPage(PortfolioViewModel vm) { InitializeComponent(); PageLifecycle.Connect(this, vm); } }
