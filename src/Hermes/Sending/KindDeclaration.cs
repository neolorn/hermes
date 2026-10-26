using System;

namespace Hermes.Sending;

/// <summary>A kind as the host declared it (<c>01</c> MSG-SEND-001), checked when the host starts.</summary>
/// <param name="Name">The kind's name.</param>
/// <param name="Category">The name of the category it belongs to.</param>
/// <param name="Email">Its email options, where it may go out by email.</param>
/// <param name="Sms">Its SMS options, where it may go out by SMS.</param>
/// <param name="Push">Its push options, where it may go out by push.</param>
/// <param name="ExpiresAfter">How long after its recording a delivery not yet sent is not sent at all, or none.</param>
internal sealed record KindDeclaration(string Name, string Category, KindEmail? Email, KindSms? Sms, KindPush? Push, TimeSpan? ExpiresAfter);