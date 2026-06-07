using WebApplication.Services.Admin;
using WebApplication.Tests.Reports;
using Xunit;

namespace WebApplication.Tests.Admin;

public sealed class ElectronicQueueAdminRepositoryIntegrationTests
{
    [Fact]
    [Trait(ElectronicQueueTestDb.RequiresDbTrait, "true")]
    public async Task ListCategoriesAsync_ReturnsActiveCategories()
    {
        if (!await ElectronicQueueTestDb.CanConnectAsync())
            return;

        var cs = ElectronicQueueTestDb.TryGetConnectionString();
        Assert.NotNull(cs);

        var repo = new ElectronicQueueAdminRepository(Microsoft.Extensions.Options.Options.Create(
            new WebApplication.Models.Configuration.ConnectionStringsOptions
            {
                ElectronicQueue = cs
            }));

        var items = await repo.ListCategoriesAsync(includeArchived: false);

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.False(i.IsArchived));
    }
}
