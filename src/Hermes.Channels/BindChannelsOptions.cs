using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Hermes.Channels;

/// <summary>Binds <see cref="ChannelsOptions"/> from the values in force, through the configuration binder's source generator.</summary>
internal sealed class BindChannelsOptions(ChannelsConfiguration state) : IConfigureOptions<ChannelsOptions>
{
    public void Configure(ChannelsOptions options)
    {
        state.Effective.Bind(options);
    }
}