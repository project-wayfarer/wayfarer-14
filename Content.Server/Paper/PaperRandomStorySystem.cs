using Content.Server._NF.Library.Systems; // Wayfarer
using Content.Server.Database; // Wayfarer
using Content.Shared.Paper;
using Content.Shared.StoryGen;
using System.Collections.Concurrent; // Wayfarer
using System.Threading.Tasks; // Wayfarer

namespace Content.Server.Paper;

public sealed class PaperRandomStorySystem : EntitySystem
{
    [Dependency] private readonly StoryGeneratorSystem _storyGen = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    // Wayfarer
    [Dependency] private readonly LibraryConsoleSystem _library = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private ConcurrentQueue<NFLibraryBook?> _books = new();
    private bool queryRunning = false;
    private const int cacheSize = 50;
    private const int cacheRefillThreshold = 20;
    // End Wayfarer

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperRandomStoryComponent, MapInitEvent>(OnMapInit);
        RefillCacheIfLow(); // Wayfarer
    }

    // Wayfarer
    private async Task RefillCacheIfLow()
    {
        if (!queryRunning && _books.Count < cacheRefillThreshold)
        {
            queryRunning = true;
            foreach (var book in await _library.GetRandomPublishedBooksAsync(cacheSize))
            {
                _books.Enqueue(book);
            }
            queryRunning = false;
        }
    }
    // End Wayfarer

    private void OnMapInit(Entity<PaperRandomStoryComponent> paperStory, ref MapInitEvent ev)
    {
        if (!TryComp<PaperComponent>(paperStory, out var paper))
            return;
        // Wayfarer
        if (_books.TryDequeue(out var book) && book != null)
        {
            _paper.SetContent((paperStory.Owner, paper), book.Content);
            _metaData.SetEntityName(paperStory.Owner, book.IsNSFW ? Loc.GetString("library-console-download-nsfw-header") + " " + book.Title : book.Title);
            if (!string.IsNullOrWhiteSpace(book.Warnings))
            {
                _metaData.SetEntityDescription(paperStory.Owner,
                    Loc.GetString("library-book-author", ("author", book.Author)) + "\n" +
                    Loc.GetString(book.IsNSFW ? "library-book-warnings-nsfw" : "library-book-warnings", ("warnings", book.Warnings)));
            }
            RefillCacheIfLow();
            return;
        }
        RefillCacheIfLow();
        // End Wayfarer
        if (!_storyGen.TryGenerateStoryFromTemplate(paperStory.Comp.Template, out var story))
            return;

        _paper.SetContent((paperStory.Owner, paper), story);
    }
}
