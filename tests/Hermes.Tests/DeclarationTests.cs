using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class DeclarationTests
{
    public static TheoryData<string, string, string> InvalidKinds => new()
    {
        { "Order.Placed", "Order.Placed", "name" },
        { "order..placed", "order..placed", "name" },
        { "order.placed.", "order.placed.", "name" },
        { "-order", "-order", "name" },
        { "", "", "name" },
        { "o" + new string('r', 64), "o" + new string('r', 64), "name" },
        { "ordér", "ordér", "name" },
    };

    [Theory]
    [MemberData(nameof(InvalidKinds))]
    public async Task MSG_SEND_001_AC2_KindNameOutsideTheRuleFailsStartup(string name, string declaration, string field)
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders");
            declare.Kind(name, "orders").Email();
        });

        AssertInvalid(failure, declaration, field);
    }

    public static TheoryData<string?, string[]?> InvalidInApps => new()
    {
        { "Orders.View", ["orderId"] },
        { "orders..view", ["orderId"] },
        { "", ["orderId"] },
        { "orders.view", null },
        { "orders.view", [] },
        { "orders.view", ["1order"] },
        { "orders.view", ["order-id"] },
        { "orders.view", ["o" + new string('r', 64)] },
        { "orders.view", ["language"] },
        { "orders.view", ["orderId", "ORDERID"] },
        { null, ["orderId"] },
    };

    [Theory]
    [MemberData(nameof(InvalidInApps))]
    public async Task MSG_INAPP_001_AC3_AnInAppTargetOrTargetValueOutsideItsRuleFailsStartupNamingTheKind(string? target, string[]? targetValues)
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential();
            declare.Kind("order.placed", "orders").InApp(target, targetValues);
        });

        AssertInvalid(failure, "order.placed", "inapp");
    }

    [Fact]
    public async Task MSG_INAPP_001_AC3_AnInAppKindWithNoTargetOrWithATargetAndItsValuesIsAccepted()
    {
        await AssertStartsPastTheDeclarations(declare =>
        {
            declare.Category("orders").Essential();
            declare.Kind("order.placed", "orders").InApp();
            declare.Kind("order.shipped", "orders").InApp("orders.view", ["orderId", "shipped_at2"]);
        });
    }

    [Fact]
    public async Task MSG_ENG_003_AC1_AStartupCodeIsThrownOnlyAsTheStartupExceptionWhileTheHostStarts()
    {
        IHost? host = null;
        Exception? registering = Record.Exception(() => host = TestHost.Build(TestHost.Languages(), declare =>
        {
            declare.Category("orders").Essential();
            declare.Kind("Order.Placed", "orders").Email();
        }));

        Assert.Null(registering);
        using IHost built = host!;
        MessagingStartupException starting = Assert.IsType<MessagingStartupException>(await Record.ExceptionAsync(() => built.StartAsync(TestContext.Current.CancellationToken)));
        Assert.Equal("messaging.startup.declarationinvalid", starting.Code);
    }

    [Fact]
    public async Task MSG_SEND_001_AC2_KindNameOfTheRuleWithEachSeparatorIsAccepted()
    {
        await AssertStartsPastTheDeclarations(declare =>
        {
            declare.Category("orders").Essential();
            declare.Kind("order.placed-v2_" + new string('x', 48), "orders").Email();
        });
    }

    [Fact]
    public async Task MSG_SEND_001_AC2_KindDeclaredTwiceFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders");
            declare.Kind("order.placed", "orders").Email();
            declare.Kind("order.placed", "orders").Sms();
        });

        AssertInvalid(failure, "order.placed", "name");
    }

    [Fact]
    public async Task MSG_SEND_001_AC2_KindBothDeclaredAndRetiredFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders");
            declare.Kind("order.placed", "orders").Email();
            declare.Retire("order.placed");
        });

        AssertInvalid(failure, "order.placed", "name");
    }

    [Fact]
    public async Task MSG_SEND_001_AC2_RetiredNameOutsideTheRuleFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare => declare.Retire("Order Placed"));

        AssertInvalid(failure, "Order Placed", "name");
    }

    [Fact]
    public async Task MSG_SEND_001_AC2_KindRetiredTwiceFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Retire("order.shipped");
            declare.Retire("order.shipped");
        });

        AssertInvalid(failure, "order.shipped", "name");
    }

    [Fact]
    public async Task MSG_SEND_001_AC2_KindOfACategoryNotDeclaredFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders");
            declare.Kind("order.placed", "billing").Email();
        });

        AssertInvalid(failure, "order.placed", "category");
    }

    [Fact]
    public async Task MSG_SEND_001_AC2_KindWithNoChannelFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders");
            declare.Kind("order.placed", "orders");
        });

        AssertInvalid(failure, "order.placed", "channel");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task MSG_SEND_001_AC2_NonPositiveExpiresAfterFailsStartup(int seconds)
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders");
            declare.Kind("order.placed", "orders").Email().ExpiresAfter(TimeSpan.FromSeconds(seconds));
        });

        AssertInvalid(failure, "order.placed", "ExpiresAfter");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task MSG_SEND_001_AC2_SmsMaxSegmentsOutsideOneToTenFailsStartup(int segments)
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential();
            declare.Kind("order.placed", "orders").Sms(maxSegments: segments);
        });

        AssertInvalid(failure, "order.placed", "maxSegments");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task MSG_SEND_001_AC2_SmsMaxSegmentsAtTheEdgeIsAccepted(int segments)
    {
        await AssertStartsPastTheDeclarations(declare =>
        {
            declare.Category("orders").Essential();
            declare.Kind("order.placed", "orders").Sms(maxSegments: segments);
        });
    }

    [Theory]
    [InlineData("00:00:00.999")]
    [InlineData("28.00:00:01")]
    public async Task MSG_SEND_001_AC2_PushTtlOutsideOneSecondToTwentyEightDaysFailsStartup(string ttl)
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential();
            declare.Kind("order.placed", "orders").Push(ttl: TimeSpan.Parse(ttl, System.Globalization.CultureInfo.InvariantCulture));
        });

        AssertInvalid(failure, "order.placed", "ttl");
    }

    [Theory]
    [InlineData("00:00:01")]
    [InlineData("28.00:00:00")]
    public async Task MSG_SEND_001_AC2_PushTtlAtTheEdgeIsAccepted(string ttl)
    {
        await AssertStartsPastTheDeclarations(declare =>
        {
            declare.Category("orders").Essential();
            declare.Kind("order.placed", "orders").Push(ttl: TimeSpan.Parse(ttl, System.Globalization.CultureInfo.InvariantCulture), urgency: PushUrgency.High);
        });
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_CategoryNameOutsideTheRuleFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare => declare.Category("Orders").Essential());

        AssertInvalid(failure, "Orders", "name");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_CategoryDeclaredTwiceFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential();
            declare.Category("orders").Essential();
        });

        AssertInvalid(failure, "orders", "name");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_EssentialCategoryWithAConsentPurposeFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare => declare.Category("security").Essential().RequiresConsent("marketing"));

        AssertInvalid(failure, "security", "RequiresConsent");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_EssentialCategoryWithQuietHoursFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare => declare.Category("security").QuietHours(true).Essential());

        AssertInvalid(failure, "security", "QuietHours");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_EssentialCategoryWithoutQuietHoursIsAccepted()
    {
        await AssertStartsPastTheDeclarations(declare =>
        {
            declare.Category("security").Essential().QuietHours(false);
            declare.Kind("sign.in", "security").Sms();
        });
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_ConsentPurposeOutsideTheNameRuleFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(
            declare => declare.Category("offers").RequiresConsent("Marketing Offers"));

        AssertInvalid(failure, "offers", "RequiresConsent");
    }

    [Theory]
    [InlineData(0, "01:00:00")]
    [InlineData(1, "00:00:59")]
    [InlineData(1, "31.00:00:01")]
    public async Task MSG_SEND_002_AC2_LimitOutsideItsRuleFailsStartup(int count, string window)
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential().Limit(Channel.Sms, count, TimeSpan.Parse(window, System.Globalization.CultureInfo.InvariantCulture));
            declare.Kind("order.placed", "orders").Sms();
        });

        AssertInvalid(failure, "orders", "Limit");
    }

    [Theory]
    [InlineData(0, "01:00:00")]
    [InlineData(1, "00:00:59")]
    [InlineData(1, "31.00:00:01")]
    public async Task MSG_SEND_002_AC2_DeploymentLimitOutsideItsRuleFailsStartup(int count, string window)
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential().DeploymentLimit(Channel.Sms, count, TimeSpan.Parse(window, System.Globalization.CultureInfo.InvariantCulture));
            declare.Kind("order.placed", "orders").Sms();
        });

        AssertInvalid(failure, "orders", "DeploymentLimit");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_LimitWindowAboveRetentionPlacesFailsStartup()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Retention:Places"] = "2.00:00:00";

        MessagingStartupException failure = await StartFailure(
            declare =>
            {
                declare.Category("orders").Essential().Limit(Channel.Sms, 3, TimeSpan.FromDays(3));
                declare.Kind("order.placed", "orders").Sms();
            },
            configuration: configuration);

        AssertInvalid(failure, "orders", "Limit");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_LimitOnAChannelNoKindOfTheCategoryUsesFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential().Limit(Channel.Push, 3, TimeSpan.FromHours(1));
            declare.Category("alerts").Essential();
            declare.Kind("order.placed", "orders").Sms();
            declare.Kind("alert.raised", "alerts").Push();
        });

        AssertInvalid(failure, "orders", "Limit");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_TwoLimitsOnOneChannelWithTheSameWindowFailStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential()
                .Limit(Channel.Sms, 3, TimeSpan.FromHours(1))
                .Limit(Channel.Sms, 5, TimeSpan.FromHours(1));
            declare.Kind("order.placed", "orders").Sms();
        });

        AssertInvalid(failure, "orders", "Limit");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_TwoDeploymentLimitsOnOneChannelWithTheSameWindowFailStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential()
                .DeploymentLimit(Channel.Sms, 300, TimeSpan.FromHours(1))
                .DeploymentLimit(Channel.Sms, 500, TimeSpan.FromHours(1));
            declare.Kind("order.placed", "orders").Sms();
        });

        AssertInvalid(failure, "orders", "DeploymentLimit");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_SeveralLimitsOnOneChannelWithTheirOwnWindowsAreAccepted()
    {
        await AssertStartsPastTheDeclarations(declare =>
        {
            declare.Category("orders").Essential()
                .Limit(Channel.Sms, 3, TimeSpan.FromHours(1))
                .Limit(Channel.Sms, 10, TimeSpan.FromDays(1))
                .Limit(Channel.Email, 10, TimeSpan.FromDays(31))
                .DeploymentLimit(Channel.Sms, 1_000, TimeSpan.FromHours(1))
                .DeploymentLimit(Channel.Sms, 1_000, TimeSpan.FromMinutes(1));
            declare.Kind("order.placed", "orders").Sms().Email();
        });
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_LabelMissingForADeclaredLanguageOfAnOptionalCategoryWithAnEmailKindFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(
            declare =>
            {
                declare.Category("news").Label("en", "News");
                declare.Kind("news.weekly", "news").Email();
            },
            configuration: Addressed(TestHost.Languages("en", "ar")));

        AssertInvalid(failure, "news", "Label");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_LabelOfEachDeclaredLanguageWhateverItsCaseIsAccepted()
    {
        await AssertStartsPastTheDeclarations(
            declare =>
            {
                declare.Category("news").Label("EN", "News").Label("ar", "الأخبار");
                declare.Kind("news.weekly", "news").Email();
            },
            Addressed(TestHost.Languages("en", "ar")));
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_LabelIsNotRequiredWithoutAnEmailKind()
    {
        await AssertStartsPastTheDeclarations(declare =>
        {
            declare.Category("news");
            declare.Kind("news.weekly", "news").Push();
        });
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_LabelOf128ScalarValuesOutsideTheBasicMultilingualPlaneIsAccepted()
    {
        string label = string.Concat(Enumerable.Repeat("\U0001F4E8", 128));
        Assert.Equal(256, label.Length);

        await AssertStartsPastTheDeclarations(
            declare =>
            {
                declare.Category("news").Label("en", label);
                declare.Kind("news.weekly", "news").Email();
            },
            Addressed(TestHost.Languages()));
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_LabelOf129ScalarValuesFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(
            declare =>
            {
                declare.Category("news").Label("en", string.Concat(Enumerable.Repeat("\U0001F4E8", 129)));
                declare.Kind("news.weekly", "news").Email();
            },
            configuration: Addressed(TestHost.Languages()));

        AssertInvalid(failure, "news", "Label");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_LabelHoldingALoneSurrogateFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(
            declare =>
            {
                declare.Category("news").Label("en", "News \uD83D");
                declare.Kind("news.weekly", "news").Email();
            },
            configuration: Addressed(TestHost.Languages()));

        AssertInvalid(failure, "news", "Label");
    }

    [Fact]
    public async Task MSG_SEND_002_AC2_LabelLongerThan128WhereNoneIsRequiredFailsStartup()
    {
        MessagingStartupException failure = await StartFailure(declare =>
        {
            declare.Category("orders").Essential().Label("en", new string('a', 129));
            declare.Kind("order.placed", "orders").Sms();
        });

        AssertInvalid(failure, "orders", "Label");
    }

    [Fact]
    public async Task OptionalCategoryWithAnEmailKind_WithoutPublicBaseAddress_StopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(
            declare =>
            {
                declare.Category("news").Label("en", "News");
                declare.Kind("news.weekly", "news").Email();
            },
            configuration: TestHost.Languages());

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal("PublicBaseAddress", failure.Details["key"]);
    }

    [Fact]
    public async Task EssentialCategoryWithAnEmailKind_WithoutPublicBaseAddress_Starts()
    {
        await AssertStartsPastTheDeclarations(declare =>
        {
            declare.Category("security").Essential();
            declare.Kind("sign.in", "security").Email(new EmailAddress { Address = "security@example.invalid", DisplayName = "Security" });
        });
    }

    [Fact]
    public async Task Category_CalledAgainOnItsBuilder_KeepsTheLastValue()
    {
        await AssertStartsPastTheDeclarations(declare =>
        {
            // Essential is set before QuietHours(true) is taken back by QuietHours(false).
            declare.Category("security").QuietHours(true).Essential().QuietHours(false);
            declare.Kind("sign.in", "security").Sms(maxSegments: 20).Sms(maxSegments: 2);
        });
    }

    private static Dictionary<string, string?> Addressed(Dictionary<string, string?> configuration)
    {
        configuration["Hermes:PublicBaseAddress"] = "https://messages.example.invalid/notifications";
        return configuration;
    }

    private static async Task<MessagingStartupException> StartFailure(Action<DeclarationBuilder> declare, Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? services = null, Dictionary<string, string?>? configuration = null)
    {
        using IHost host = TestHost.Build(configuration ?? Addressed(TestHost.Languages()), declare, services);
        return await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));
    }

    private static async Task AssertStartsPastTheDeclarations(Action<DeclarationBuilder> declare, Dictionary<string, string?>? configuration = null)
    {
        using IHost host = TestHost.Build(configuration ?? Addressed(TestHost.Languages()), declare);
        MessagingStartupException? failure = await Record.ExceptionAsync(() => host.StartAsync(TestContext.Current.CancellationToken)) as MessagingStartupException;

        Assert.True(
            failure is null || failure.Code is not ("messaging.startup.declarationinvalid" or "messaging.startup.configurationinvalid"),
            $"The start stopped at the declarations: {failure?.Message}");
    }

    private static void AssertInvalid(MessagingStartupException failure, string declaration, string field)
    {
        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal(declaration, failure.Details["declaration"]);
        Assert.Equal(field, failure.Details["field"]);
    }
}