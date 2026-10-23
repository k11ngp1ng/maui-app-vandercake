using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Views;
public partial class OrdersPage : ContentPage { public OrdersPage(OrdersViewModel vm) { InitializeComponent(); PageLifecycle.Connect(this, vm); } }
