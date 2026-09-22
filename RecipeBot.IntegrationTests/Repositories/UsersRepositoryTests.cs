using AutoFixture;
using Microsoft.EntityFrameworkCore;
using RecipeBot.Infrastructure.Repositories;

namespace RecipeBot.IntegrationTests.Repositories;

[TestFixture]
public class UsersRepositoryTests : RepositoryTestBase
{
    [Test]
    public async Task GetOrCreate_NewUser_PersistsUser()
    {
        var chatId = Fixture.Create<long>();
        var name = Fixture.Create<string>();
        var repository = new UsersRepository(Factory);

        var result = await repository.GetOrCreateUserAsync(chatId, name, CancellationToken.None);

        await using var db = await Factory.CreateDbContextAsync();
        var saved = await db.Users.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.GreaterThan(0));
            Assert.That(saved.Id, Is.EqualTo(result.Id));
            Assert.That(saved.TelegramId, Is.EqualTo(chatId));
            Assert.That(saved.Name, Is.EqualTo(name));
        });
    }

    [Test]
    public async Task GetOrCreate_ExistingUser_ReturnsSameUserWithoutOverwritingName()
    {
        var data = await SeedOwners();
        var repository = new UsersRepository(Factory);

        var result = await repository.GetOrCreateUserAsync(
            data.Owner.TelegramId, Fixture.Create<string>(), CancellationToken.None);

        await using var db = await Factory.CreateDbContextAsync();
        var saved = await db.Users.SingleAsync(u => u.TelegramId == data.Owner.TelegramId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(data.Owner.Id));
            Assert.That(result.Name, Is.EqualTo(data.Owner.Name));
            Assert.That(saved.Name, Is.EqualTo(data.Owner.Name));
        });
        Assert.That(await db.Users.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetByChatId_ReturnsMatchingUser()
    {
        var data = await SeedOwners();
        var result = await new UsersRepository(Factory).GetByChatId(
            data.Owner.TelegramId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(data.Owner.Id));
            Assert.That(result.Name, Is.EqualTo(data.Owner.Name));
            Assert.That(result.TelegramId, Is.EqualTo(data.Owner.TelegramId));
        });
    }

    [Test]
    public void GetByChatId_MissingUser_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => new UsersRepository(Factory)
            .GetByChatId(Fixture.Create<long>(), CancellationToken.None));
    }
}
