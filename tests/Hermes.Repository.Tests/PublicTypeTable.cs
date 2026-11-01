using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Repository.Tests;

/// <summary>
/// The public types the tables of <c>04</c> MSG-LIB-002 and MSG-LIB-008 give, by package, each
/// in its package's namespace (<c>07</c> MSG-ENG-001), a case nested in the type it is a case
/// of, as the public API files write it.
/// </summary>
internal static class PublicTypeTable
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> Types = new Dictionary<string, string[]>
    {
        [Packages.Core] =
        [
            // Values and identifiers.
            "Send",
            "SendValue", "SendValue.PlainText", "SendValue.WholeNumber", "SendValue.DecimalNumber", "SendValue.Flag", "SendValue.Instant", "SendValue.List",
            "Recipient", "Recipient.Person", "Recipient.Anonymous",
            "SendId", "DeliveryId", "EntryId", "ConversationId", "MessageId", "ParticipantId", "SavedReplyId",
            "SendReceipt", "DeliveryReceipt",
            "SendRecord", "DeliveryRecord", "AttemptRecord", "CallRecord",
            "Result", "Result`1", "Failure",
            "MessagingStartupException",
            "EmailAddress",
            "CategoryPreferences", "ChannelPreference",
            "SubjectExport", "SubscriptionExport", "DeliveryExport", "InAppEntryExport", "IdempotencyKeyExport",
            "InAppPage", "InAppEntry",
            "ConversationScope", "ConversationPage", "ConversationRecord", "ParticipantRecord", "MessagePage", "MessageRecord", "SavedReplyRecord",
            "Channel", "DeliveryStatus", "AttemptClass", "AttemptReason", "PushUrgency", "PushService", "PushPlatform", "SmsEncoding", "SmsReportOutcome", "ConsentAnswer",
            "ConversationType", "ConversationStatus", "MessageType", "ScopeKind", "AuthorKind",

            // Operations.
            "IMessages", "IPreferences", "ISuppressions", "ISubjects", "IInApp", "IConversations",

            // Provider contracts.
            "Attempt", "Attempt.Accepted", "Attempt.Transient", "Attempt.Permanent", "Attempt.Gone",
            "IEmailProvider", "IEmailSession", "EmailMessage",
            "ISmsProvider", "BalanceReading", "BalanceReading.Balance", "BalanceReading.NotSupported", "BalanceReading.Failed", "SmsMessage",
            "SmsText", "SmsMeasure",
            "SmsReportRequest", "SmsReportReading", "SmsReportResponse",
            "IPushProvider", "PushRequest", "PushRequest.WebPush",

            // Seams a host supplies.
            "ISecretSource", "SecretVersions", "SecretVersion",
            "IRecipientIdentity",
            "IConversationMembership",
            "IConsentSource", "ConsentQuery",
            "IContentProtector", "UnprotectResult", "UnprotectResult.Opened", "UnprotectResult.Erased", "UnprotectResult.KeyUnavailable", "UnprotectResult.Rejected",
            "ITemplateSource", "TemplateKey",
            "IDeliveryObserver", "DeliveryNotice",
            "IAlertSink",
        ],
        [Packages.Channels] = ["MessagingChannelsServiceCollectionExtensions"],
        [Packages.Engine] =
        [
            "MessagingServiceCollectionExtensions", "DeclarationBuilder", "KindBuilder", "CategoryBuilder",
            "MessagingEndpointRouteBuilderExtensions", "IDispatcher",
        ],
        [Packages.Testing] =
        [
            "InMemoryEmailProvider", "InMemorySmsProvider", "InMemoryPushProvider", "FixedSecretSource", "TestDispatcher",
            "MessagingTestDatabase", "MessagingTestingServiceCollectionExtensions",
        ],
        [Packages.Cli] = [],
        [Packages.Development] =
        [
            "MessagingDevelopmentExtensions", "DevelopmentInbox", "InboxFilter", "InboxMessage", "InboxEmail", "InboxSms", "InboxPush", "InboxNotification",
        ],
        [Packages.Conversations] = ["MessagingConversationsServiceCollectionExtensions", "MessagingConversationsEndpointRouteBuilderExtensions"],
    }.ToFrozenDictionary(
        entry => entry.Key,
        entry => entry.Value.Select(type => entry.Key + "." + type).ToFrozenSet(StringComparer.Ordinal),
        StringComparer.Ordinal);

    /// <summary>Answers each public type of <paramref name="package"/> whose name the tables do not give.</summary>
    internal static IEnumerable<string> Violations(string package, IEnumerable<PublicType> types)
    {
        FrozenSet<string> given = Types.GetValueOrDefault(package, FrozenSet<string>.Empty);
        return types.Select(type => type.Name)
            .Where(name => !given.Contains(name))
            .Order(StringComparer.Ordinal)
            .Select(name => $"{package} declares the public type {name}, which no table of 04 MSG-LIB-002 or MSG-LIB-008 gives.");
    }
}