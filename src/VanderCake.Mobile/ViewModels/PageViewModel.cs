using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VanderCake.Mobile.Client;
namespace VanderCake.Mobile.ViewModels;
public abstract class PageViewModel(IAppNavigation navigation) : ObservableObject
{
    private bool busy;
    private string error = "", notice = "";
    private CancellationTokenSource? operation;
    private bool cancelledByUser;
    protected IAppNavigation Navigation { get; } = navigation;
    public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); OnPropertyChanged(nameof(IsIdle)); } }
    public bool IsIdle => !IsBusy;
    public string Error { get => error; protected set { SetProperty(ref error, value); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string Notice { get => notice; protected set { SetProperty(ref notice, value); OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => !string.IsNullOrEmpty(Notice);
    public IAsyncRelayCommand RefreshCommand => new AsyncRelayCommand(LoadAsync);
    public IRelayCommand CancelCommand => new RelayCommand(Cancel);
    public virtual Task LoadAsync() => Task.CompletedTask;
    public void Cancel() { cancelledByUser = true; operation?.Cancel(); }
    public void Deactivate() => operation?.Cancel();
    protected async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; Error = ""; Notice = ""; cancelledByUser = false;
        using var source = new CancellationTokenSource(); operation = source;
        try { await action(source.Token); }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        { if (cancelledByUser) Notice = "Request cancelled. For an order, retry the same submission or check history before creating another."; }
        catch (ClientFailure e) { Error = e.Message; if (e.AuthenticationExpired) await Navigation.LoginAsync(); }
        catch (Exception) { Error = "Something went wrong. Please try again. If submitting an order, check its status before starting another."; }
        finally { operation = null; IsBusy = false; }
    }
}
