using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Persistence;
using JellyFish.Api.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;

namespace JellyFish.Api.Tests.Infrastructure.Repositories;

/// <summary>
/// A pattern is identified by folder + name, so the same name can live in several folders
/// (Mongo enforces this with the compound unique index in IndexInitializer).
/// </summary>
public class PatternNamesAcrossFoldersTests
{
    private static JellyfishDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<JellyfishDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new InMemoryJellyfishDbContext(options);
    }

    /// <summary>
    /// The Mongo provider stores nested objects as embedded documents; the in-memory provider
    /// can't map them without help. Keep only folders, patterns and details, map PatternDetails
    /// as owned types, and skip the LedOnPos dictionary (not used here).
    /// </summary>
    private sealed class InMemoryJellyfishDbContext(DbContextOptions<JellyfishDbContext> options)
        : JellyfishDbContext(options, Options.Create(new MongoSettings()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<Zone>();
            modelBuilder.Ignore<Schedule>();
            modelBuilder.Ignore<ActiveZone>();
            modelBuilder.Ignore<AuditLog>();
            modelBuilder.Ignore<Holiday>();
            modelBuilder.Entity<PatternDetail>().OwnsOne(p => p.Details, d =>
            {
                d.Ignore(x => x.LedOnPos);
                d.OwnsOne(x => x.RunData);
                d.OwnsOne(x => x.FireworkSettings);
            });
        }
    }

    [Fact]
    public async Task Patterns_With_Same_Name_In_Different_Folders_Are_Kept_Separately()
    {
        await using var db = NewContext();
        var repo = new PatternRepository(db);

        await repo.AddAsync(Pattern.Create("Christmas", "Candy Cane", isReadOnly: false).Value);
        await repo.AddAsync(Pattern.Create("Holidays", "Candy Cane", isReadOnly: true).Value);
        await db.SaveChangesAsync();

        (await repo.ExistsAsync("Christmas", "Candy Cane")).ShouldBeTrue();
        (await repo.ExistsAsync("Holidays", "Candy Cane")).ShouldBeTrue();
        (await repo.ExistsAsync("Halloween", "Candy Cane")).ShouldBeFalse();

        (await repo.GetAsync("Christmas", "Candy Cane")).Value.IsReadOnly.ShouldBeFalse();
        (await repo.GetAsync("Holidays", "Candy Cane")).Value.IsReadOnly.ShouldBeTrue();

        (await repo.ListByFolderAsync("Christmas")).Value.ShouldHaveSingleItem().FolderName.ShouldBe("Christmas");
    }

    [Fact]
    public async Task Deleting_A_Pattern_Leaves_The_Same_Name_In_Another_Folder()
    {
        await using var db = NewContext();
        var repo = new PatternRepository(db);

        await repo.AddAsync(Pattern.Create("Christmas", "Candy Cane", false).Value);
        await repo.AddAsync(Pattern.Create("Holidays", "Candy Cane", false).Value);
        await db.SaveChangesAsync();

        (await repo.DeleteAsync("Christmas", "Candy Cane")).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync();

        (await repo.ExistsAsync("Christmas", "Candy Cane")).ShouldBeFalse();
        (await repo.ExistsAsync("Holidays", "Candy Cane")).ShouldBeTrue();
    }

    [Fact]
    public async Task Upserting_A_Detail_Updates_Only_The_Pattern_In_Its_Folder()
    {
        await using var db = NewContext();
        var repo = new PatternDetailRepository(db);

        await repo.UpsertAsync(PatternDetail.Create("Christmas", "Candy Cane", new PatternDetails { Type = "Color", Colors = [255, 0, 0] }).Value);
        await repo.UpsertAsync(PatternDetail.Create("Holidays", "Candy Cane", new PatternDetails { Type = "Chase", Colors = [0, 255, 0] }).Value);
        await db.SaveChangesAsync();

        await repo.UpsertAsync(PatternDetail.Create("Christmas", "Candy Cane", new PatternDetails { Type = "Twinkle", Colors = [255, 255, 255] }).Value);
        await db.SaveChangesAsync();

        (await repo.ListAsync()).Count.ShouldBe(2);
        (await repo.GetAsync("Christmas", "Candy Cane")).Value.Details.Type.ShouldBe("Twinkle");
        (await repo.GetAsync("Holidays", "Candy Cane")).Value.Details.Type.ShouldBe("Chase");
    }
}
