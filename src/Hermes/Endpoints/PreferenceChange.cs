namespace Hermes.Endpoints;

/// <summary>The body of <c>PUT /preferences/{category}/{channel}</c>; a body without <c>on</c> does not read.</summary>
/// <param name="On">Whether the person receives the category on the channel.</param>
internal sealed record PreferenceChange(bool? On);