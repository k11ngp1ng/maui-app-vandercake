using CommunityToolkit.Mvvm.Input;
using VanderCake.Contracts.Api;
using VanderCake.Mobile.Client;
namespace VanderCake.Mobile.ViewModels;
public sealed class OrdersViewModel : PageViewModel
{
    private readonly ISalesApi api;
    private List<OrderResponse> orders = [], all = [];
    private string filter = "All";
    public List<OrderResponse> Orders { get => orders; private set => SetProperty(ref orders, value); }
    public string[] Statuses { get; } = ["All", "Submitted", "Fulfilled", "PartiallyFulfilled", "Cut"];
    public string Filter { get => filter; set { SetProperty(ref filter, value); ApplyFilter(); } }
    public IAsyncRelayCommand<OrderResponse> OpenCommand { get; }
    public OrdersViewModel(ISalesApi api, IAppNavigation nav) : base(nav)
    { this.api = api; OpenCommand = new AsyncRelayCommand<OrderResponse>(o => o is null ? Task.CompletedTask : Navigation.OpenAsync("order", o.Id)); }
    private void ApplyFilter() => Orders = all.Where(o => Filter == "All" || o.Status == Filter).ToList();
    public override Task LoadAsync() => RunAsync(async ct => { all = await api.OrdersAsync(ct); ApplyFilter(); });
}
public sealed class OrderViewModel(ISalesApi api, IAppNavigation nav) : PageViewModel(nav)
{
    private OrderResponse? order;
    public Guid Id { get; set; }
    public OrderResponse? Order { get => order; private set => SetProperty(ref order, value); }
    public override Task LoadAsync() => RunAsync(async ct => Order = await api.OrderAsync(Id, ct));
}
