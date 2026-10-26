namespace Hermes.Channels;

/// <summary>The codes of <c>08</c> section 1 the channels' startup checks throw.</summary>
internal static class ChannelCodes
{
    internal const string DeclarationMissing = "messaging.startup.declarationmissing";

    internal const string ConfigurationInvalid = "messaging.startup.configurationinvalid";

    internal const string SecretUnavailable = "messaging.startup.secretunavailable";

    internal const string SecretInvalid = "messaging.startup.secretinvalid";
}