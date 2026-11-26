using Orbs.Application.Abstractions;
using Orbs.Domain.Context;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;

namespace Orbs.Application.Demo;

/// <summary>
/// Fills an empty install with one demo provider, one note and one orb,
/// so a first run shows something alive without any API key.
/// </summary>
public sealed class DemoSeeder(
    IOrbRepository orbs,
    IModelProviderRepository providers,
    IContextSourceRepository contextSources,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string EchoKind = "echo";

    public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
    {
        if ((await orbs.ListAsync(cancellationToken)).Count > 0 || (await providers.ListAsync(cancellationToken)).Count > 0)
        {
            return false;
        }

        var now = clock.GetUtcNow();

        var echo = ModelProvider.Create("Echo (demo)", EchoKind, now);
        echo.SetDefaultModel("echo-1", now);
        providers.Add(echo);

        var note = ContextSource.Create("About Orbs", "text", now);
        note.Configure(null, WelcomeNote, now);
        note.MarkSynced(WelcomeNote, now);
        contextSources.Add(note);

        var pip = Orb.Create("Pip", now);
        pip.Describe("A friendly guide to Orbs", now);
        pip.Brief("Lead with the answer, keep it short and friendly.", now);
        pip.LinkContext(note.Id, now);
        orbs.Add(pip);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private const string WelcomeNote =
        """
        Orbs is an open-source home for AI companions.
        An orb is a model, its context and a face.
        Connect any model provider under Models, add context under Context,
        and give every orb its own look in the orb editor.
        The demo provider "Echo" repeats what you say, so you can try Orbs without an API key.
        """;
}
