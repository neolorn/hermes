using System;
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// The engine's keys of the configuration section <c>Hermes</c> (<c>08</c> section 4), bound
/// by the configuration binder's source generator from the values
/// <see cref="ConfigurationState"/> holds in force (<c>06</c> MSG-OPS-001).
/// </summary>
internal sealed class MessagingOptions
{
    public Uri? PublicBaseAddress { get; set; }

    public string[] Languages { get; set; } = [];

    public string? DefaultLanguage { get; set; }

    [Required]
    public string TimeZone { get; set; } = "UTC";

    [ValidateObjectMembers]
    public TemplatesOptions Templates { get; set; } = new();

    public MessagesOptions Messages { get; set; } = new();

    [ValidateObjectMembers]
    public DispatchOptions Dispatch { get; set; } = new();

    [ValidateObjectMembers]
    public ObserverOptions Observer { get; set; } = new();

    [ValidateObjectMembers]
    public RetryOptions Retry { get; set; } = new();

    [ValidateObjectMembers]
    public IdempotencyOptions Idempotency { get; set; } = new();

    [ValidateObjectMembers]
    public QuietHoursOptions QuietHours { get; set; } = new();

    [ValidateObjectMembers]
    public RetentionOptions Retention { get; set; } = new();

    [ValidateObjectMembers]
    public JobsOptions Jobs { get; set; } = new();

    [ValidateObjectMembers]
    public DatabaseOptions Database { get; set; } = new();

    [ValidateObjectMembers]
    public AlertsOptions Alerts { get; set; } = new();

    [ValidateObjectMembers]
    public UnsubscribeOptions Unsubscribe { get; set; } = new();

    [ValidateObjectMembers]
    public SecretsOptions Secrets { get; set; } = new();

    public InAppOptions InApp { get; set; } = new();
}