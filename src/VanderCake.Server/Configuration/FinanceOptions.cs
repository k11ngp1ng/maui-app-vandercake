using System.ComponentModel.DataAnnotations;
using VanderCake.Domain.Finance;

namespace VanderCake.Server.Configuration;

public sealed class FinanceOptions
{
    [Range(0, 10000)] public int PenaltyBasisPoints { get; set; } = 500;
    [Range(0, 10000)] public int DailyInterestBasisPoints { get; set; } = 200;
    [Range(0, 10000)] public int DailyDiscountBasisPoints { get; set; } = 100;
    public FinancialRules ToRules() => new(PenaltyBasisPoints, DailyInterestBasisPoints, DailyDiscountBasisPoints);
}
