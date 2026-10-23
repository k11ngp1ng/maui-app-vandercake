using CommunityToolkit.Mvvm.Input;
using VanderCake.Contracts.Api;
using VanderCake.Mobile.Client;
namespace VanderCake.Mobile.ViewModels;
public sealed class CustomerViewModel : PageViewModel
{
    private readonly ISalesApi api;
    private CustomerResponse? customer;
    private CustomerBalanceResponse? balance;
    public Guid Id { get; set; }
    public CustomerResponse? Customer { get => customer; private set => SetProperty(ref customer, value); }
    public CustomerBalanceResponse? Balance { get => balance; private set => SetProperty(ref balance, value); }
    public IAsyncRelayCommand PurchasesCommand { get; }
    public IAsyncRelayCommand NewOrderCommand { get; }
    public CustomerViewModel(ISalesApi api, IAppNavigation nav) : base(nav)
    {
        this.api = api;
        PurchasesCommand = new AsyncRelayCommand(() => Navigation.OpenAsync("purchases", Id));
        NewOrderCommand = new AsyncRelayCommand(() => Navigation.OpenAsync("order-new", Id));
    }
    public override Task LoadAsync() => RunAsync(async ct => { Customer = await api.CustomerAsync(Id, ct); Balance = await api.BalanceAsync(Id, ct); });
}
public sealed class PurchasesViewModel(ISalesApi api, IAppNavigation nav) : PageViewModel(nav)
{
    private List<SaleResponse> purchases = [];
    public Guid Id { get; set; }
    public List<SaleResponse> Purchases { get => purchases; private set => SetProperty(ref purchases, value); }
    public override Task LoadAsync() => RunAsync(async ct => Purchases = await api.PurchasesAsync(Id, ct));
}
