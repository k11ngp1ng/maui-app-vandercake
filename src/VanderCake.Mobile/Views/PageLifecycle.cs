using VanderCake.Mobile.ViewModels;
namespace VanderCake.Mobile.Views;
public static class PageLifecycle
{
    public static void Connect(ContentPage page, PageViewModel vm)
    {
        page.BindingContext = vm;
        page.Appearing += async (_, _) => await vm.LoadAsync();
        page.Disappearing += (_, _) => vm.Deactivate();
    }
    public static Guid Id(IDictionary<string, object> query) => query.TryGetValue("id", out var value) && Guid.TryParse(value.ToString(), out var id) ? id : Guid.Empty;
}
