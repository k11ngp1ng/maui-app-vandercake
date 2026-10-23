using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Views;
public partial class CustomerPage : ContentPage, IQueryAttributable
{
    private readonly CustomerViewModel vm;
    public CustomerPage(CustomerViewModel vm) { InitializeComponent(); this.vm = vm; PageLifecycle.Connect(this, vm); }
    public void ApplyQueryAttributes(IDictionary<string, object> query) => vm.Id = PageLifecycle.Id(query);
}
