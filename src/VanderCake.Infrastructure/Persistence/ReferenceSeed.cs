using Microsoft.EntityFrameworkCore;
using VanderCake.Domain.Customers;
using VanderCake.Domain.Finance;

namespace VanderCake.Infrastructure.Persistence;

public static class ReferenceSeed
{
    public static readonly Guid SaoPauloStateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid MinasGeraisStateId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid SaoPauloCityId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid CampinasCityId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid BeloHorizonteCityId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    internal static void Configure(ModelBuilder b)
    {
        b.Entity<State>().HasData(
            new { Id = SaoPauloStateId, Version = SaoPauloStateId, Code = "SP", Name = "São Paulo" },
            new { Id = MinasGeraisStateId, Version = MinasGeraisStateId, Code = "MG", Name = "Minas Gerais" });
        b.Entity<City>().HasData(
            new { Id = SaoPauloCityId, Version = SaoPauloCityId, StateId = SaoPauloStateId, Name = "São Paulo" },
            new { Id = CampinasCityId, Version = CampinasCityId, StateId = SaoPauloStateId, Name = "Campinas" },
            new { Id = BeloHorizonteCityId, Version = BeloHorizonteCityId, StateId = MinasGeraisStateId, Name = "Belo Horizonte" });
        b.Entity<PaymentTerm>().HasData(
            new { Code = 10, Name = "50% in 7 and 14 days" },
            new { Code = 20, Name = "33.33%, 33.33%, 33.34% in 7, 14 and 21 days" });
        b.Entity<InstallmentDefinition>().HasData(
            new { PaymentTermCode = 10, Number = 1, Days = 7, BasisPoints = 5000 },
            new { PaymentTermCode = 10, Number = 2, Days = 14, BasisPoints = 5000 },
            new { PaymentTermCode = 20, Number = 1, Days = 7, BasisPoints = 3333 },
            new { PaymentTermCode = 20, Number = 2, Days = 14, BasisPoints = 3333 },
            new { PaymentTermCode = 20, Number = 3, Days = 21, BasisPoints = 3334 });
    }
}
