namespace Hermes.Sending;

/// <summary>
/// Registered by <c>AddHermesTesting</c> (<c>04</c> MSG-LIB-008): only a test's passes dispatch,
/// so <c>AddHermes</c> registers neither the hosted dispatcher nor the <c>observer</c> job, and a
/// pass delivers, after its deliveries, every pending notice in order, as the job does.
/// </summary>
internal sealed class TestPasses
{
}