using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Views;
public partial class PurchasesPage : ContentPage, IQueryAttributable
{
    private readonly PurchasesViewModel vm;
    public PurchasesPage(PurchasesViewModel vm) { InitializeComponent(); this.vm = vm; PageLifecycle.Connect(this, vm); }
    public void ApplyQueryAttributes(IDictionary<string, object> query) => vm.Id = PageLifecycle.Id(query);
}
