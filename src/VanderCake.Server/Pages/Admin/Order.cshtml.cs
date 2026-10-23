using VanderCake.Application.Abstractions;
using VanderCake.Contracts.Api;
namespace VanderCake.Server.Pages.Admin;
public sealed class OrderModel(IOrderService orders) : AdminPageModel
{
    public OrderResponse Order { get; private set; } = null!;
    public async Task OnGetAsync(Guid id, CancellationToken ct) => Order = await orders.GetAsync(id, ct);
}
