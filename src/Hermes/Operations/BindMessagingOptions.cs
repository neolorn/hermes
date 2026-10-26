using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>Binds <see cref="MessagingOptions"/> from the values in force, through the configuration binder's source generator.</summary>
internal sealed class BindMessagingOptions(ConfigurationState state) : IConfigureOptions<MessagingOptions>
{
    public void Configure(MessagingOptions options)
    {
        state.Effective.Bind(options);
    }
}