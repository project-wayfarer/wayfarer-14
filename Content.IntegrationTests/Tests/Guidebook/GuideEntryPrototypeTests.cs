using Content.Client.Guidebook;
using Content.Client.Guidebook.Richtext;
using Robust.Shared.ContentPack;
using Robust.Shared.Log; // Coyote
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Guidebook;

namespace Content.IntegrationTests.Tests.Guidebook;

[TestFixture]
[TestOf(typeof(GuidebookSystem))]
[TestOf(typeof(GuideEntryPrototype))]
[TestOf(typeof(DocumentParsingManager))]
public sealed class GuideEntryPrototypeTests
{
    // Coyote: Hey you, stop it. Are you going to modify this file?
    // Only overwrite it with upstream's if we have a version of RT with https://github.com/space-wizards/RobustToolbox/pull/6443
    // Otherwise, do not touch this.
    [Test]
    [Description("Ensures all guidebook entries are valid, checking the document/etc.")]
    public async Task ValidateAllGuideEntries()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitIdleAsync();

        // Coyote Start
        // Suppress UI style update limit warnings. I hate this solution, but it is what it is, champ. We're overriding the log level and putting it back where it was once we're done.
        var logMan = client.ResolveDependency<ILogManager>();
        var uiLog = logMan.GetSawmill("ui");
        var oldLevel = uiLog.Level;
        uiLog.Level = LogLevel.Error;

        try
        {
            var protoMan = client.ResolveDependency<IPrototypeManager>();
            var resMan = client.ResolveDependency<IResourceManager>();
            var parser = client.ResolveDependency<DocumentParsingManager>();

            var prototypes = protoMan.EnumeratePrototypes<GuideEntryPrototype>().ToList();
            Assert.That(prototypes, Is.Not.Empty, "No guidebook entries found.");

            foreach (var proto in prototypes)
            {
                await client.WaitAssertion(() =>
                {
                    using var reader = resMan.ContentFileReadText(proto.Text);
                    var text = reader.ReadToEnd();
                    Assert.That(parser.TryAddMarkup(new Document(), text),
                        $"Failed to parse guidebook entry: {proto.Id}");
                });

                // Give the UI a tick to process any pending updates
                await client.WaitRunTicks(1);
            }
        }
        finally
        {
            // Restore original log level
            uiLog.Level = oldLevel;
            await pair.CleanReturnAsync();
        }
        // End Coyote
    }
}
