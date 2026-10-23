using Microsoft.Extensions.Logging;
using VanderCake.Mobile.Client;
using VanderCake.Mobile.Services;
using VanderCake.Mobile.ViewModels;
using VanderCake.Mobile.Views;
namespace VanderCake.Mobile;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold"); });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IConnectionSettings, ConnectionSettings>();
        builder.Services.AddSingleton<ITokenVault, TokenVault>();
        builder.Services.AddSingleton(sp => ApiConnection.CreateClient(sp.GetRequiredService<IConnectionSettings>()));
        builder.Services.AddSingleton<ISalesApi, SalesApi>(); builder.Services.AddSingleton<IAppNavigation, AppNavigation>();
        builder.Services.AddTransient<LoginViewModel>(); builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<PortfolioViewModel>(); builder.Services.AddSingleton<PortfolioPage>();
        builder.Services.AddTransient<CustomerViewModel>(); builder.Services.AddTransient<CustomerPage>();
        builder.Services.AddTransient<PurchasesViewModel>(); builder.Services.AddTransient<PurchasesPage>();
        builder.Services.AddTransient<RegistrationViewModel>(); builder.Services.AddTransient<RegistrationPage>();
        builder.Services.AddTransient<NewOrderViewModel>(); builder.Services.AddTransient<NewOrderPage>();
        builder.Services.AddTransient<OrdersViewModel>(); builder.Services.AddTransient<OrdersPage>();
        builder.Services.AddTransient<OrderViewModel>(); builder.Services.AddTransient<OrderPage>();
        builder.Services.AddSingleton<AppShell>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
