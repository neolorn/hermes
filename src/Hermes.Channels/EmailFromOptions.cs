namespace Hermes.Channels;

/// <summary><c>Hermes:Email:From</c>, the deployment's default sender (<c>02</c> MSG-EMAIL-002).</summary>
internal sealed class EmailFromOptions
{
    public string? Address { get; set; }

    public string? DisplayName { get; set; }
}