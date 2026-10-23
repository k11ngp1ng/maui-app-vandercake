using VanderCake.Mobile.Views;
using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile;
public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        SignIn.ContentTemplate = new DataTemplate(() => services.GetRequiredService<LoginPage>());
        Portfolio.ContentTemplate = new DataTemplate(() => services.GetRequiredService<PortfolioPage>());
        Routing.RegisterRoute("customer", typeof(CustomerPage)); Routing.RegisterRoute("purchases", typeof(PurchasesPage));
        Routing.RegisterRoute("register", typeof(RegistrationPage)); Routing.RegisterRoute("order-new", typeof(NewOrderPage));
        Routing.RegisterRoute("orders", typeof(OrdersPage)); Routing.RegisterRoute("order", typeof(OrderPage));
        Navigating += async (_, args) =>
        {
            if (!args.CanCancel || args.Source is not (ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot)
                || CurrentPage?.BindingContext is not NewOrderViewModel { HasPendingSubmission: true } vm) return;
            if (vm.IsBusy) { args.Cancel(); return; }
            var deferral = args.GetDeferral();
            try
            {
                if (!await DisplayAlertAsync("Leave this submission?",
                    "The server may already have accepted it. Stay here to retry the same request. If you leave, inspect history before creating another order.",
                    "Leave and check history", "Stay here")) args.Cancel();
            }
            finally { deferral.Complete(); }
        };
    }
}
