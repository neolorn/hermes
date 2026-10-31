namespace Hermes;

/// <summary>The codes of <c>08</c> section 1 the engine answers or throws.</summary>
internal static class Codes
{
    internal const string DeclarationInvalid = "messaging.startup.declarationinvalid";

    internal const string DeclarationMissing = "messaging.startup.declarationmissing";

    internal const string ConfigurationInvalid = "messaging.startup.configurationinvalid";

    internal const string SecretUnavailable = "messaging.startup.secretunavailable";

    internal const string SecretInvalid = "messaging.startup.secretinvalid";

    internal const string SchemaMismatch = "messaging.startup.schemamismatch";

    internal const string TooLong = "messaging.send.toolong";

    internal const string RenderLimit = "messaging.send.renderlimit";

    internal const string RenderFailed = "messaging.send.renderfailed";

    internal const string NoDestination = "messaging.send.nodestination";

    internal const string DestinationInvalid = "messaging.send.destinationinvalid";

    internal const string DestinationNotAllowed = "messaging.send.destinationnotallowed";

    internal const string IdempotencyConflict = "messaging.send.idempotencyconflict";

    internal const string SendUnknown = "messaging.send.unknown";

    internal const string KeyUnavailable = "messaging.key.unavailable";

    internal const string ReleaseNewer = "messaging.release.newer";

    internal const string PreferenceUnknown = "messaging.preference.unknown";

    internal const string PreferenceEssential = "messaging.preference.essential";

    internal const string PreferenceFixed = "messaging.preference.fixed";

    internal const string InAppCursorInvalid = "messaging.inapp.cursorinvalid";

    internal const string InAppUnknown = "messaging.inapp.unknown";

    internal const string SuppressionEssential = "messaging.suppression.essential";

    internal const string SuppressionInvalid = "messaging.suppression.invalid";

    internal const string RequestMalformed = "messaging.request.malformed";

    internal const string RequestUnauthenticated = "messaging.request.unauthenticated";

    internal const string RequestTooLarge = "messaging.request.toolarge";

    internal const string RequestMediaType = "messaging.request.mediatype";

    internal const string RequestUnavailable = "messaging.request.unavailable";

    internal const string RequestRateLimited = "messaging.request.ratelimited";

    internal const string UnsubscribeInvalid = "messaging.unsubscribe.invalid";

    internal const string PushEndpointNotAllowed = "messaging.push.endpointnotallowed";

    internal const string PushKeyUnknown = "messaging.push.keyunknown";
}