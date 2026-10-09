using SteamApiClient.Contracts.SteamStoreApi;
using System.Text.Json;

namespace SteamApiClient.Tests;

public sealed class AppDetailsDeserializationTests
{
    // Matches the options used by SteamStoreClient.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal const string RealisticPayload = """
        {
          "success": true,
          "data": {
            "type": "game",
            "name": "Example Game",
            "steam_appid": 400,
            "required_age": 0,
            "is_free": false,
            "dlc": [],
            "detailed_description": "Full description",
            "about_the_game": "About this game",
            "short_description": "An example game",
            "supported_languages": "English",
            "header_image": "https://cdn.example.invalid/header.jpg",
            "website": null,
            "pc_requirements": [],
            "mac_requirements": { "minimum": "Minimum Mac requirements", "recommended": "Recommended Mac requirements" },
            "linux_requirements": [],
            "developers": ["Example Developer"],
            "publishers": ["Example Publisher"],
            "price_overview": {
              "currency": "USD", "initial": 1999, "final": 1499, "discount_percent": 25,
              "initial_formatted": "$19.99", "final_formatted": "$14.99"
            },
            "packages": [12345],
            "package_groups": [
              {
                "name": "default",
                "title": "Buy Example Game",
                "description": "Purchase options",
                "selection_text": "Select a purchase option",
                "save_text": "Save 25%",
                "display_type": 0,
                "is_recurring_subscription": "false",
                "subs": [
                  {
                    "packageid": 12345,
                    "percent_savings_text": "25%",
                    "percent_savings": 25,
                    "option_text": "Example Game - $14.99",
                    "option_description": "Base game",
                    "can_get_free_license": "0",
                    "is_free_license": false,
                    "price_in_cents_with_discount": 1499
                  }
                ]
              }
            ],
            "platforms": { "windows": true, "mac": false, "linux": true },
            "categories": [], "genres": [], "screenshots": [],
            "release_date": { "coming_soon": false, "date": "9 Oct, 2026" },
            "background_raw": ""
          }
        }
        """;

    [Fact]
    public void RealisticAppDetailsPreservesPackageGroupsAndSubsAlongsideOtherFields()
    {
        var response = JsonSerializer.Deserialize<AppDetailsResponse>(RealisticPayload, JsonOptions)!;
        Assert.True(response.Success);
        var data = Assert.IsType<AppData>(response.AppData);
        var group = Assert.Single(Assert.IsType<List<PackageGroups>>(data.PackageGroups));
        Assert.Equal("default", group.Name);
        Assert.Equal("Buy Example Game", group.Title);
        Assert.Equal("Purchase options", group.Description);
        Assert.Equal("Select a purchase option", group.SelectionText);
        Assert.Equal("Save 25%", group.SaveText);
        Assert.Equal(0, group.DisplayType);
        Assert.Equal("false", group.IsRecurringSubscription);
        var sub = Assert.Single(Assert.IsType<Sub[]>(group.Subs));
        Assert.Equal(12345, sub.PackageId);
        Assert.Equal("25%", sub.PercentSavingsText);
        Assert.Equal(25, sub.PercentSavings);
        Assert.Equal("Example Game - $14.99", sub.OptionText);
        Assert.Equal("Base game", sub.OptionDescription);
        Assert.Equal("0", sub.CanGetFreeLicense);
        Assert.False(sub.IsFreeLicense);
        Assert.Equal(1499, sub.PriceInCentsWithDiscount);

        Assert.Equal(12345, Assert.Single(data.Packages!));
        Assert.True(data.Platforms!.Windows);
        Assert.False(data.Platforms.Mac);
        Assert.True(data.Platforms.Linux);
        Assert.Equal("USD", data.PriceOverview!.Currency);
        Assert.Equal(1999, data.PriceOverview.Initial);
        Assert.Equal(1499, data.PriceOverview.Final);
        Assert.Equal(25, data.PriceOverview.DiscountPercent);
        Assert.Equal("$19.99", data.PriceOverview.InitialFormatted);
        Assert.Equal("$14.99", data.PriceOverview.FinalFormatted);
        Assert.Null(data.PcRequirements);
        Assert.Null(data.LinuxRequirements);
        Assert.Equal("Minimum Mac requirements", data.MacRequirements!.Minimum);
        Assert.Equal("Recommended Mac requirements", data.MacRequirements.Recommended);
        Assert.Equal("0", data.RequiredAge);
    }

    [Fact]
    public void MultipleGroupsAndSubsPreserveOrderAndIndependentValues()
    {
        const string payload = """
            {
              "success": true,
              "data": {
                "package_groups": [
                  { "name": "base", "subs": [
                    { "packageid": 12345, "percent_savings": 25, "option_text": "Base", "is_free_license": false, "price_in_cents_with_discount": 1499 },
                    { "packageid": 23456, "percent_savings": 100, "option_text": "Free demo", "can_get_free_license": "1", "is_free_license": true, "price_in_cents_with_discount": 0 }
                  ] },
                  { "name": "deluxe", "display_type": 1, "is_recurring_subscription": "true", "subs": [
                    { "packageid": 34567, "percent_savings": 10, "option_text": "Deluxe", "is_free_license": false, "price_in_cents_with_discount": 2999 }
                  ] }
                ]
              }
            }
            """;
        var data = JsonSerializer.Deserialize<AppDetailsResponse>(payload, JsonOptions)!.AppData!;
        var groups = Assert.IsType<List<PackageGroups>>(data.PackageGroups);
        Assert.Equal(new[] { "base", "deluxe" }, groups.Select(group => group.Name));
        var subs = groups[0].Subs!;
        Assert.Equal(new[] { 12345, 23456 }, subs.Select(sub => sub.PackageId));
        Assert.Equal(25, subs[0].PercentSavings);
        Assert.Equal(1499, subs[0].PriceInCentsWithDiscount);
        Assert.Equal("Free demo", subs[1].OptionText);
        Assert.Equal(100, subs[1].PercentSavings);
        Assert.Equal("1", subs[1].CanGetFreeLicense);
        Assert.True(subs[1].IsFreeLicense);
        Assert.Equal(0, subs[1].PriceInCentsWithDiscount);
        var deluxe = groups[1];
        Assert.Equal(1, deluxe.DisplayType);
        Assert.Equal("true", deluxe.IsRecurringSubscription);
        var deluxeSub = Assert.Single(deluxe.Subs!);
        Assert.Equal(34567, deluxeSub.PackageId);
        Assert.Equal("Deluxe", deluxeSub.OptionText);
        Assert.Equal(10, deluxeSub.PercentSavings);
        Assert.Equal(2999, deluxeSub.PriceInCentsWithDiscount);
    }

    [Theory]
    [InlineData("\"package_groups\": []", true)]
    [InlineData("\"package_groups\": null", false)]
    [InlineData("", false)]
    public void EmptyMissingAndNullPackageGroupsHaveStandardListSemantics(string property, bool isEmptyList)
    {
        var payload = "{\"success\":true,\"data\":{" + property + "}}";
        var data = JsonSerializer.Deserialize<AppDetailsResponse>(payload, JsonOptions)!.AppData!;
        if (isEmptyList)
            Assert.Empty(Assert.IsType<List<PackageGroups>>(data.PackageGroups));
        else
            Assert.Null(data.PackageGroups);
    }
}
