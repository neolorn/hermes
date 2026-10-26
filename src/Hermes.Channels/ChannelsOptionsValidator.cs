using Microsoft.Extensions.Options;

namespace Hermes.Channels;

/// <summary>
/// The source-generated validation of <see cref="ChannelsOptions"/>, the second line behind
/// the checks on the raw values (<c>06</c> MSG-OPS-001).
/// </summary>
[OptionsValidator]
internal sealed partial class ChannelsOptionsValidator : IValidateOptions<ChannelsOptions>;