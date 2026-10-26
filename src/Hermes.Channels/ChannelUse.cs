namespace Hermes.Channels;

/// <summary>
/// Which channels an application uses, and which shipped providers, as <c>04</c> MSG-LIB-003
/// defines them; a channel's own keys are read and checked where it is used, and a shipped
/// provider's keys and secrets where that provider is (<c>08</c> section 4).
/// </summary>
/// <param name="ProvidersAlone">Whether the application registers the providers alone, so the channels read <c>Dispatch:Timeout</c> themselves.</param>
/// <param name="Email">Whether email is used.</param>
/// <param name="Sms">Whether SMS is used.</param>
/// <param name="Push">Whether push is used.</param>
/// <param name="ShippedPush">Whether the shipped push provider is used.</param>
internal sealed record ChannelUse(bool ProvidersAlone, bool Email, bool Sms, bool Push, bool ShippedPush);