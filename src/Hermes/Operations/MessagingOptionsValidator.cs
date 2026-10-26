using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// The source-generated validation of <see cref="MessagingOptions"/>, the second line behind
/// the checks on the raw values (<c>06</c> MSG-OPS-001).
/// </summary>
[OptionsValidator]
internal sealed partial class MessagingOptionsValidator : IValidateOptions<MessagingOptions>;