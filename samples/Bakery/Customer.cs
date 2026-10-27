namespace Bakery;

/// <summary>A person the bakery knows: a customer, with the address and number its notices go to, or a member of staff.</summary>
internal sealed record Customer(string Id, string Email, string Phone, bool Staff);