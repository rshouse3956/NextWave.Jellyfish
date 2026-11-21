using JellyFish.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace JellyFish.Api.Tests.Infrastructure.Persistence;

public class MongoSettingsTests
{
    /// <summary>Binds the Mongo section the way Program.cs does.</summary>
    private static MongoSettings Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build()
            .GetSection(MongoSettings.SectionName).Get<MongoSettings>() ?? new MongoSettings();

    [Fact]
    public void Missing_DatabaseName_UsesDefault()
    {
        Bind(new() { ["Mongo:ConnectionString"] = "mongodb://x" })
            .DatabaseName.ShouldBe(MongoSettings.DefaultDatabaseName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_DatabaseName_UsesDefault(string blank)
    {
        // e.g. Mongo__DatabaseName=${DB_NAME} with DB_NAME unset
        Bind(new() { ["Mongo:DatabaseName"] = blank })
            .DatabaseName.ShouldBe(MongoSettings.DefaultDatabaseName);
    }

    [Fact]
    public void Set_DatabaseName_IsUsed()
    {
        Bind(new() { ["Mongo:DatabaseName"] = " jellyfish-local " })
            .DatabaseName.ShouldBe("jellyfish-local");
    }

    [Fact]
    public void Default_Is_Jellyfish()
    {
        new MongoSettings().DatabaseName.ShouldBe("jellyfish");
    }
}
