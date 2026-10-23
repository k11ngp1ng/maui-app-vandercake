using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.Input;
using VanderCake.Mobile.Client;
namespace VanderCake.Mobile.ViewModels;
public sealed class LoginViewModel : PageViewModel
{
    private readonly ISalesApi api;
    private readonly IConnectionSettings settings;
    private string email = "", password = "", address, pin;
    public string Email { get => email; set => SetProperty(ref email, value); }
    public string Password { get => password; set => SetProperty(ref password, value); }
    public string Address { get => address; set => SetProperty(ref address, value); }
    public string DevelopmentPin { get => pin; set => SetProperty(ref pin, value); }
    public bool IsDevelopment => settings.IsDevelopment;
    public IAsyncRelayCommand LoginCommand { get; }
    public LoginViewModel(ISalesApi api, IConnectionSettings settings, IAppNavigation navigation) : base(navigation)
    {
        this.api = api; this.settings = settings; address = settings.Address; pin = settings.DevelopmentPin;
        LoginCommand = new AsyncRelayCommand(SignInAsync);
    }
    public override Task LoadAsync() => RunAsync(async ct => { Password = ""; if (await api.RestoreAsync(ct)) await Navigation.PortfolioAsync(); });
    public Task SignInAsync() => RunAsync(async ct => {
        if (!new EmailAddressAttribute().IsValid(Email.Trim()) || string.IsNullOrEmpty(Password)) throw new ClientFailure("Enter your email and password.");
        var origin = ApiConnection.Validate(Address); settings.Address = origin.AbsoluteUri; settings.DevelopmentPin = DevelopmentPin.Trim();
        try { await api.LoginAsync(Email.Trim(), Password, ct); await Navigation.PortfolioAsync(); }
        finally { Password = ""; }
    });
}
