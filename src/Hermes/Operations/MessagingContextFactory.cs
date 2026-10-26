using Microsoft.EntityFrameworkCore.Design;

namespace Hermes.Operations;

/// <summary>Makes the context for EF Core's tool when it generates a migration; it never connects.</summary>
internal sealed class MessagingContextFactory : IDesignTimeDbContextFactory<MessagingContext>
{
    public MessagingContext CreateDbContext(string[] args)
    {
        return MessagingContext.Create("Host=localhost");
    }
}