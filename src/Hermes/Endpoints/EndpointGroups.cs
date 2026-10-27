namespace Hermes.Endpoints;

/// <summary>
/// The endpoint groups the application maps (<c>04</c> MSG-LIB-004), recorded by the mapping
/// calls, which a host makes before it starts, so the startup checks that depend on them run
/// after its groups are mapped (<c>06</c> MSG-OPS-001).
/// </summary>
internal sealed class EndpointGroups
{
    internal bool Preferences { get; set; }

    internal bool PushSubscriptions { get; set; }

    internal bool Unsubscribe { get; set; }

    internal bool SmsReports { get; set; }

    /// <summary>Whether a mapped group needs the identity seam.</summary>
    internal bool NeedIdentity => Preferences || PushSubscriptions;
}