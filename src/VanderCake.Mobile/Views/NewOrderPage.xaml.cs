using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Views;
public partial class NewOrderPage : ContentPage, IQueryAttributable
{
    private readonly NewOrderViewModel vm;
    public NewOrderPage(NewOrderViewModel vm) { InitializeComponent(); this.vm = vm; PageLifecycle.Connect(this, vm); }
    public void ApplyQueryAttributes(IDictionary<string, object> query) => vm.Id = PageLifecycle.Id(query);
}
