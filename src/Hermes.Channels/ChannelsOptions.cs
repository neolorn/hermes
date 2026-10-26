using Microsoft.Extensions.Options;

namespace Hermes.Channels;

/// <summary>
/// The channels' keys of the configuration section <c>Hermes</c> (<c>08</c> section 4), bound
/// by the configuration binder's source generator from the values
/// <see cref="ChannelsConfiguration"/> holds in force (<c>06</c> MSG-OPS-001).
/// </summary>
internal sealed class ChannelsOptions
{
    [ValidateObjectMembers]
    public ChannelsDispatchOptions Dispatch { get; set; } = new();

    [ValidateObjectMembers]
    public EmailChannelOptions Email { get; set; } = new();

    [ValidateObjectMembers]
    public SmsChannelOptions Sms { get; set; } = new();

    [ValidateObjectMembers]
    public PushChannelOptions Push { get; set; } = new();
}