using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Views;
public partial class OrderPage : ContentPage, IQueryAttributable
{
    private readonly OrderViewModel vm;
    public OrderPage(OrderViewModel vm) { InitializeComponent(); this.vm = vm; PageLifecycle.Connect(this, vm); }
    public void ApplyQueryAttributes(IDictionary<string, object> query) => vm.Id = PageLifecycle.Id(query);
}
