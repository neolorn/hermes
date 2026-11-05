namespace Hermes.Sms.Cequens;

/// <summary>One reason the gateway refused a request.</summary>
/// <param name="Code">The reason's numeric code, such as <c>-12</c> for a sender name not registered.</param>
internal sealed record InternalError(int? Code);