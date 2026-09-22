using Microsoft.EntityFrameworkCore;
using RecipeBot.Infrastructure.Repositories;

namespace RecipeBot.IntegrationTests.Repositories;

[TestFixture]
public class CategoryRepositoryTests : RepositoryTestBase
{
    [Test]
    public async Task GetAllCategories()
    {
        var categories = Enumerable.Range(0, Random.Shared.Next(2, 11))
            .Select(_ => NewCategory()).ToArray();
        await using (var db = await Factory.CreateDbContextAsync())
        {
            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();
        }

        var result = await new CategoryRepository(Factory).GetAllCategories(CancellationToken.None);

        Assert.That(result.Select(c => (c.Id, c.Name)),
            Is.EquivalentTo(categories.Select(c => (c.Id, c.Name))));
    }
}
