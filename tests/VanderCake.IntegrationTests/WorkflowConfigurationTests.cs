using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VanderCake.Server.Configuration;

namespace VanderCake.IntegrationTests;

public sealed class WorkflowConfigurationTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public async Task FIN03_InvalidConfiguredRatesRefuseStartup(int rate)
    {
        await using var factory = new ApiFactory(configure: services =>
            services.PostConfigure<FinanceOptions>(o => o.DailyInterestBasisPoints = rate));
        try { Assert.Throws<OptionsValidationException>(() => factory.Client()); }
        finally { factory.DeleteDatabase(); }
    }
}
