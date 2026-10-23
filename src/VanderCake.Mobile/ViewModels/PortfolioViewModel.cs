using CommunityToolkit.Mvvm.Input;
using VanderCake.Contracts.Api;
using VanderCake.Mobile.Client;
namespace VanderCake.Mobile.ViewModels;
public sealed class PortfolioViewModel : PageViewModel
{
    private readonly ISalesApi api;
    private List<CustomerResponse> all = [], customers = [];
    private string search = "";
    public List<CustomerResponse> Customers { get => customers; private set => SetProperty(ref customers, value); }
    public string Search { get => search; set { SetProperty(ref search, value); Filter(); } }
    public IAsyncRelayCommand<CustomerResponse> OpenCustomerCommand { get; }
    public IAsyncRelayCommand RegisterCommand { get; }
    public IAsyncRelayCommand OrdersCommand { get; }
    public IAsyncRelayCommand LogoutCommand { get; }
    public PortfolioViewModel(ISalesApi api, IAppNavigation navigation) : base(navigation)
    {
        this.api = api;
        OpenCustomerCommand = new AsyncRelayCommand<CustomerResponse>(c => c is null ? Task.CompletedTask : Navigation.OpenAsync("customer", c.Id));
        RegisterCommand = new AsyncRelayCommand(() => Navigation.OpenAsync("register"));
        OrdersCommand = new AsyncRelayCommand(() => Navigation.OpenAsync("orders"));
        LogoutCommand = new AsyncRelayCommand(() => RunAsync(async ct => {
            if (!await Navigation.ConfirmAsync("Sign out?", "This removes the saved session and revokes existing server sessions for this account. You will need to sign in again.", "Sign out")) return;
            try { await api.LogoutAsync(ct); } finally { Clear(); await Navigation.LoginAsync(); }
        }));
    }
    public void Clear() { all = []; Customers = []; Search = ""; Error = ""; Notice = ""; }
    private void Filter() => Customers = all.Where(c => string.IsNullOrWhiteSpace(Search) || c.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)).ToList();
    public override Task LoadAsync() => RunAsync(async ct => { all = await api.CustomersAsync(ct); Filter(); });
}

