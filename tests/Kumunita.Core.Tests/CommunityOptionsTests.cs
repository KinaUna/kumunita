using Kumunita.Core;
using Microsoft.Extensions.Configuration;

namespace Kumunita.Core.Tests;

public class CommunityOptionsTests
{
    [Fact]
    public void Defaults_To_PlatformName_When_Unconfigured()
    {
        var options = new CommunityOptions();

        Assert.Equal("Kumunita", options.Name);
    }

    [Fact]
    public void Binds_From_Community_ConfigurationSection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Community:Name"] = "Maplewood Residents",
                ["Community:SupportEmail"] = "maplewood@example.com",
            })
            .Build();

        var options = config.GetSection(CommunityOptions.SectionName).Get<CommunityOptions>()!;

        Assert.Equal("Maplewood Residents", options.Name);
        Assert.Equal("maplewood@example.com", options.SupportEmail);
    }
}
