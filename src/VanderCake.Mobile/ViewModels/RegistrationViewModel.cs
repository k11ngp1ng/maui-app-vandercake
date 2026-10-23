using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.Input;
using VanderCake.Contracts.Api;
using VanderCake.Mobile.Client;
namespace VanderCake.Mobile.ViewModels;
public sealed class RegistrationViewModel : PageViewModel
{
    private readonly ISalesApi api;
    private List<CityResponse> cities = [];
    private List<StateResponse> states = [];
    private StateResponse? state;
    private CityResponse? city;
    private List<CityResponse> allCities = [];
    private string name = "", address = "", phone = "", visitOrder = "1";
    public string Name { get => name; set => SetProperty(ref name, value); }
    public string Address { get => address; set => SetProperty(ref address, value); }
    public string Phone { get => phone; set => SetProperty(ref phone, value); }
    public string VisitOrder { get => visitOrder; set => SetProperty(ref visitOrder, value); }
    public List<StateResponse> States { get => states; private set => SetProperty(ref states, value); }
    public List<CityResponse> Cities { get => cities; private set => SetProperty(ref cities, value); }
    public StateResponse? SelectedState { get => state; set { SetProperty(ref state, value); Cities = allCities.Where(c => c.StateId == value?.Id).ToList(); SelectedCity = null; } }
    public CityResponse? SelectedCity { get => city; set => SetProperty(ref city, value); }
    public IAsyncRelayCommand SaveCommand { get; }
    public RegistrationViewModel(ISalesApi api, IAppNavigation nav) : base(nav) { this.api = api; SaveCommand = new AsyncRelayCommand(SaveAsync); }
    public override Task LoadAsync() => RunAsync(async ct => { States = await api.StatesAsync(ct); allCities = await api.CitiesAsync(ct); Cities = allCities.Where(c => c.StateId == SelectedState?.Id).ToList(); });
    public Task SaveAsync() => RunAsync(async ct => {
        if (!int.TryParse(VisitOrder, out var visit)) throw new ClientFailure("Enter a whole positive visit sequence.");
        var request = new CustomerRequest(Name.Trim(), SelectedCity?.Id ?? Guid.Empty, visit, Address.Trim(), Phone.Trim());
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true) || SelectedCity is null)
            throw new ClientFailure(string.Join(" ", errors.Select(e => e.ErrorMessage)) + " Choose a state and city.");
        if (!await Navigation.ConfirmAsync("Register customer?", $"Create {request.Name} in your assigned portfolio?", "Register")) return;
        var customer = await api.CreateCustomerAsync(request, ct);
        Name = Address = Phone = ""; Notice = "Customer registered successfully.";
        await Navigation.BackAsync(); await Navigation.OpenAsync("customer", customer.Id);
    });
}
