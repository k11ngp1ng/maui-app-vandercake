using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanderCake.Contracts.Api;
using VanderCake.Mobile.Client;
namespace VanderCake.Mobile.ViewModels;
// UI edit state around a shared DTO, never an alternate business model.
public sealed class ProductQuantity(ProductResponse product, Action changed) : ObservableObject
{
    private string quantity = "0";
    public ProductResponse Product { get; } = product;
    public string Quantity { get => quantity; set { SetProperty(ref quantity, value); changed(); } }
}
public sealed class NewOrderViewModel : PageViewModel
{
    private readonly ISalesApi api;
    private CustomerResponse? customer;
    private List<ProductQuantity> products = [];
    private List<PaymentTermResponse> terms = [];
    private PaymentTermResponse? term;
    private DateTime dispatchDate;
    private string window = "19:00";
    private CreateOrderRequest? pending;
    public Guid Id { get; set; }
    public CustomerResponse? Customer { get => customer; private set => SetProperty(ref customer, value); }
    public List<ProductQuantity> Products { get => products; private set => SetProperty(ref products, value); }
    public List<PaymentTermResponse> Terms { get => terms; private set => SetProperty(ref terms, value); }
    public PaymentTermResponse? SelectedTerm { get => term; set => SetProperty(ref term, value); }
    public DateTime DispatchDate { get => dispatchDate; set => SetProperty(ref dispatchDate, value); }
    public string DispatchWindow { get => window; set => SetProperty(ref window, value); }
    public string[] Windows { get; } = ["12:00", "19:00"];
    public bool HasPendingSubmission => pending is not null;
    public bool IsEditable => IsIdle && !HasPendingSubmission;
    public string Estimate => "Estimated " + Products.Sum(p => int.TryParse(p.Quantity, out var q) && q > 0 ? p.Product.PriceMinor / 100m * q : 0m).ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
    public IAsyncRelayCommand SubmitCommand { get; }
    public IAsyncRelayCommand ResetCommand { get; }
    public NewOrderViewModel(ISalesApi api, IAppNavigation nav, TimeProvider clock) : base(nav)
    {
        this.api = api;
        DispatchDate = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).Date.AddDays(1);
        SubmitCommand = new AsyncRelayCommand(SubmitAsync);
        ResetCommand = new AsyncRelayCommand(() => RunAsync(async _ => {
            if (await Navigation.ConfirmAsync("Unlock draft?", "First inspect order history. An interrupted submission may already exist. Unlock only after reconciling it with the administrator.", "Unlock draft"))
            { pending = null; OnPropertyChanged(nameof(HasPendingSubmission)); OnPropertyChanged(nameof(IsEditable)); Notice = "Draft unlocked. Review all fields before submitting."; }
        }));
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsIdle)) OnPropertyChanged(nameof(IsEditable)); };
    }
    public override Task LoadAsync() => RunAsync(async ct => {
        if (Products.Count > 0) return;
        Customer = await api.CustomerAsync(Id, ct);
        var loadedProducts = await api.ProductsAsync(ct);
        var loadedTerms = await api.TermsAsync(ct);
        Products = loadedProducts.Select(p => new ProductQuantity(p, () => OnPropertyChanged(nameof(Estimate)))).ToList();
        Terms = loadedTerms; OnPropertyChanged(nameof(Estimate));
    });
    public Task SubmitAsync() => RunAsync(async ct => {
        if (pending is null)
        {
            var lines = new List<OrderLineRequest>();
            foreach (var p in Products)
            {
                if (!int.TryParse(p.Quantity, out var quantity) || quantity < 0) throw new ClientFailure($"Enter whole non-negative units for {p.Product.Name}.");
                if (quantity == 0) continue;
                var line = new OrderLineRequest(p.Product.Id, quantity); Validate(line); lines.Add(line);
            }
            if (SelectedTerm is null || !Windows.Contains(DispatchWindow)) throw new ClientFailure("Choose payment terms and a dispatch window.");
            var request = new CreateOrderRequest(Id, SelectedTerm.Code, DateOnly.FromDateTime(DispatchDate), int.Parse(DispatchWindow[..2], CultureInfo.InvariantCulture), Guid.NewGuid(), lines);
            Validate(request);
            if (!await Navigation.ConfirmAsync("Submit order?", $"{Customer?.Name}\n{Estimate}\nDispatch {request.DispatchDate:yyyy-MM-dd} at {DispatchWindow}.\nPrices, eligibility and totals are confirmed by the server. Submitted orders cannot be edited.", "Submit")) return;
            pending = request; OnPropertyChanged(nameof(HasPendingSubmission)); OnPropertyChanged(nameof(IsEditable));
        }
        else if (!await Navigation.ConfirmAsync("Retry submission?", "Send exactly the same order with its original key. The server will return the original result if it already accepted this request.", "Retry same order")) return;
        Notice = "Keep this screen open after network failures. Retry uses the original request; do not create another order blindly.";
        var result = await api.SubmitAsync(pending, ct);
        pending = null; Notice = "Order submitted successfully.";
        OnPropertyChanged(nameof(HasPendingSubmission)); OnPropertyChanged(nameof(IsEditable));
        await Navigation.BackAsync(); await Navigation.OpenAsync("order", result.Id);
    });
    private static void Validate(object value)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), errors, true)) throw new ClientFailure(string.Join(" ", errors.Select(e => e.ErrorMessage)));
    }
}

