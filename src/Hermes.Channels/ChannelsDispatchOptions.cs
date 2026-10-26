using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Channels;

/// <summary><c>Hermes:Dispatch:Timeout</c>, which the shipped providers read wherever they are used (<c>08</c> section 4).</summary>
internal sealed class ChannelsDispatchOptions
{
    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);
}