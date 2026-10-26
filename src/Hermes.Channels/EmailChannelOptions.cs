using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Channels;

/// <summary><c>Hermes:Email</c>, the email channel's own keys and the SMTP server's (<c>08</c> section 4).</summary>
internal sealed class EmailChannelOptions
{
    public string? Host { get; set; }

    [Range(1, 65_535)]
    public int? Port { get; set; }

    public EmailSecurity Security { get; set; } = EmailSecurity.ImplicitTls;

    public string? TrustedAuthority { get; set; }

    public EmailRevocation TrustedAuthorityRevocation { get; set; } = EmailRevocation.Published;

    [Range(typeof(TimeSpan), "1.00:00:00", "10.00:00:00")]
    public TimeSpan ShortLivedMaxValidity { get; set; } = TimeSpan.FromDays(7);

    public EmailFromOptions From { get; set; } = new();

    [Range(1, 1_000)]
    public int Rate { get; set; } = 10;

    [Range(10 * 1024, 25 * 1024 * 1024)]
    public int MaxBytes { get; set; } = 1024 * 1024;

    /// <summary>The port in force: <c>Email:Port</c>, or 465 with implicit TLS and 587 with <c>STARTTLS</c>.</summary>
    internal int PortInForce => Port ?? (Security == EmailSecurity.ImplicitTls ? 465 : 587);
}

/// <summary>How the SMTP session takes TLS (<c>02</c> MSG-EMAIL-001).</summary>
internal enum EmailSecurity
{
    /// <summary>TLS from the connection's start (RFC 8314).</summary>
    ImplicitTls,

    /// <summary>TLS by <c>STARTTLS</c>, required.</summary>
    StartTls,
}

/// <summary>Whether <c>Email:TrustedAuthority</c> publishes revocation (<c>02</c> MSG-EMAIL-001).</summary>
internal enum EmailRevocation
{
    /// <summary>Every certificate below it that names no revocation source fails, the short-lived leaf apart.</summary>
    Published,

    /// <summary>A certificate below it that names no revocation source is accepted.</summary>
    None,
}