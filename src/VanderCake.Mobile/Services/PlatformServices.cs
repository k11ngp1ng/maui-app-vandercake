using VanderCake.Mobile.Client;
using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Services;
public sealed class ConnectionSettings : IConnectionSettings
{
    private static string DefaultAddress => Environment.GetEnvironmentVariable("VANDERCAKE_API_URL") ?? (DeviceInfo.Platform == DevicePlatform.Android ? (DeviceInfo.DeviceType == DeviceType.Virtual ? "https://10.0.2.2:7080/" : "") : "https://localhost:7080/");
    public string Address { get => Preferences.Default.Get("ApiAddress", DefaultAddress); set => Preferences.Default.Set("ApiAddress", value); }
    public string DevelopmentPin { get => Preferences.Default.Get("DevelopmentPin", ""); set => Preferences.Default.Set("DevelopmentPin", value); }
    public bool IsDevelopment {
        get {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}
public sealed class TokenVault : ITokenVault
{
    private const string Key = "VanderCake.Session.v1";
    public async Task<string?> ReadAsync()
    {
        try { return await SecureStorage.Default.GetAsync(Key); }
        catch { Clear(); throw new ClientFailure("Secure session storage is unavailable. Restart the app or sign in again."); }
    }
    public async Task WriteAsync(string value)
    {
        try { await SecureStorage.Default.SetAsync(Key, value); }
        catch { Clear(); throw new ClientFailure("The session could not be stored securely. Sign-in was cancelled; check this device's secure storage."); }
    }
    public void Clear() => SecureStorage.Default.Remove(Key);
}
public sealed class AppNavigation(IServiceProvider services) : IAppNavigation
{
    public async Task LoginAsync() { services.GetRequiredService<PortfolioViewModel>().Clear(); await Shell.Current.GoToAsync("//login"); }
    public Task PortfolioAsync() => Shell.Current.GoToAsync("//portfolio");
    public Task OpenAsync(string route, Guid? id = null) => id is { } value ? Shell.Current.GoToAsync(route, new Dictionary<string, object> { ["id"] = value }) : Shell.Current.GoToAsync(route);
    public Task BackAsync() => Shell.Current.GoToAsync("..");
    public Task<bool> ConfirmAsync(string title, string message, string accept) => Shell.Current.DisplayAlertAsync(title, message, accept, "Cancel");
}

