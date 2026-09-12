using Content.Server._NF.Library.Components;
using Content.Server.Administration.Logs; // Wayfarer
using Content.Server.Database;
using Content.Server.GameTicking; // Wayfarer
using Content.Server.Popups;
using Content.Shared._NF.Library; // Wayfarer
using Content.Shared._NF.Library.BUI;
using Content.Shared._NF.Library.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Paper;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using System.Linq; // Wayfarer
using System.Threading.Tasks; // Wayfarer

namespace Content.Server._NF.Library.Systems;

/// <summary>
/// Handles the library console. Opens the UI, allows the user to upload and download books.
/// </summary>
public sealed class LibraryConsoleSystem : EntitySystem
{
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly ServerDbEntryManager _serverDbEntry = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    private const string BookSlotId = "bookConsole_Book";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LibraryConsoleComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<LibraryConsoleComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<LibraryConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryConsoleUploadBookMessage>(OnUploadBook);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryConsoleDownloadBookMessage>(OnDownloadBook);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryConsoleReUploadBookMessage>(OnReUploadBook); // Wayfarer
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryConsoleDeleteOwnedBookMessage>(OnDeleteBook); // Wayfarer
        SubscribeLocalEvent<LibraryConsoleComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<LibraryConsoleComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<LibraryConsoleComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
    }

    private void OnComponentInit(Entity<LibraryConsoleComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent, BookSlotId, ent.Comp.BookSlot);
    }

    private void OnComponentRemove(Entity<LibraryConsoleComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent, ent.Comp.BookSlot);
    }

    private void OnSlotChanged(EntityUid uid, LibraryConsoleComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != component.BookSlot.ID)
            return;

        UpdateUiState((uid, component));
    }

    private void OnUiOpened(Entity<LibraryConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUiState(ent);
    }

    // Wayfarer
    private void OnDeleteBook(Entity<LibraryConsoleComponent> ent, ref LibraryConsoleDeleteOwnedBookMessage args)
    {
        _ = DeleteBookAsync(ent, args.Id, args.Actor);

    }

    private void OnReUploadBook(Entity<LibraryConsoleComponent> ent, ref LibraryConsoleReUploadBookMessage args)
    {
        _ = ReUploadBookAsync(ent, args.Id, args.Content, args.Actor);
    }
    // EndWayfarer

    private void OnUploadBook(Entity<LibraryConsoleComponent> ent, ref LibraryConsoleUploadBookMessage args)
    {
        if (!_playerManager.TryGetSessionByEntity(args.Actor, out var session))
            return;

        if (string.IsNullOrWhiteSpace(args.Title) ||
            string.IsNullOrWhiteSpace(args.Author) ||
            string.IsNullOrWhiteSpace(args.Content))
        {
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            _popup.PopupEntity(Loc.GetString("library-console-upload-missing-fields"), args.Actor, args.Actor);
            return;
        }

        if (args.Title.Length > LibraryBookLimits.MaxTitleLength ||
            args.Author.Length > LibraryBookLimits.MaxAuthorLength ||
            args.Content.Length > LibraryBookLimits.MaxContentLength ||
            args.Warnings.Length > LibraryBookLimits.MaxContentWarningLength) // Wayfarer
        {
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            _popup.PopupEntity(Loc.GetString("library-console-upload-too-long",
                ("maxTitle", LibraryBookLimits.MaxTitleLength),
                ("maxAuthor", LibraryBookLimits.MaxAuthorLength),
                ("maxContent", LibraryBookLimits.MaxContentLength)), args.Actor, args.Actor);
            return;
        }

        var title = args.Title.Trim(); // Wayfarer
        var author = args.Author.Trim(); // Wayfarer
        var content = args.Content;
        var authorPlayerUserId = session.UserId.UserId;
        var date = DateTime.UtcNow;
        // Wayfarer
        var isNSFW = args.IsNSFW;

        if (title.StartsWith("[NSFW]", StringComparison.InvariantCultureIgnoreCase))
        {
            title = title.Substring(6).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
                _popup.PopupEntity(Loc.GetString("library-console-upload-missing-fields"), args.Actor, args.Actor);
                return;
            }
            isNSFW = true;
        }
        // End Wayfarer

        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
        _ = AddBookAsync(ent, args.Actor, title, author, content, args.Warnings, isNSFW, args.IsPublished, date, authorPlayerUserId); // Wayfarer
    }

    private async Task AddBookAsync(Entity<LibraryConsoleComponent> ent, EntityUid actor, string title, string author, string content, string warnings, bool isNsfw, bool isPublished, DateTime date, Guid authorPlayerUserId) // Wayfarer
    {
        var server = await _serverDbEntry.ServerEntity;

        // Reject uploads whose content already exists in the library.
        var existingBooks = await _dbManager.GetNFLibraryBooksAsync();
        if (existingBooks.Any(b => string.Equals(b.Content, content, StringComparison.Ordinal)))
        {
            if (EntityManager.EntityExists(ent))
            {
                _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
                _popup.PopupEntity(Loc.GetString("library-console-upload-duplicate"), actor, actor);
            }
            return;
        }

        await _dbManager.AddNFLibraryBookAsync(_gameTicker.RoundId, server.Id, title, author, content, warnings, isNsfw, isPublished, date, authorPlayerUserId); // Wayfarer
        _adminLog.Add(LogType.Action,
            LogImpact.Medium,
            $"{ToPrettyString(actor):player} uploaded book \"{title}\" by \"{author}\" to {ToPrettyString(ent):entity}");

        // Refresh the UI so the browse tab shows the newly uploaded book.
        if (EntityManager.EntityExists(ent))
            UpdateUiState(ent);
    }

    private void OnDownloadBook(Entity<LibraryConsoleComponent> ent, ref LibraryConsoleDownloadBookMessage args)
    {
        _ = DownloadBookAsync(ent, args.BookId);
    }

    // Wayfarer
    /// <summary>
    /// Get a random book from the player library
    /// </summary>
    /// <param name="count">The minimum number of books. Prevents pulling the same book repeatedly if there are only a few</param>
    /// <returns></returns>
    public async Task<List<NFLibraryBook?>> GetRandomPublishedBooksAsync(int count = 1)
    {
        return await _dbManager.GetRandomPublishedNFLibraryBooksAsync(count);
    }
    // End Wayfarer

    private async Task DownloadBookAsync(Entity<LibraryConsoleComponent> ent, int bookId)
    {
        //var books = await _dbManager.GetNFLibraryBooksAsync();
        //var book = books.FirstOrDefault(b => b.Id == bookId);
        var book = await _dbManager.GetNFLibraryBookByIdAsync(bookId); // Wayfarer

        if (book == null || !EntityManager.EntityExists(ent))
            return;

        if (ent.Comp.BookSlot.Item is not { } bookEntity ||
            !TryComp<PaperComponent>(bookEntity, out var paper))
        {
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            return;
        }

        _paper.SetContent((bookEntity, paper), book.Content);
        // Wayfarer
        _metaData.SetEntityName(bookEntity, book.IsNSFW ? Loc.GetString("library-console-download-nsfw-header") + " " + book.Title : book.Title);
        if (!string.IsNullOrWhiteSpace(book.Warnings))
        {
            _metaData.SetEntityDescription(bookEntity, Loc.GetString(book.IsNSFW ? "library-book-warnings-nsfw" : "library-book-warnings", ("warnings", book.Warnings)));
        }
        // End Wayfarer
        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);

        // Refresh UI so the inserted book's content field stays in sync.
        UpdateUiState(ent);
    }

    // Wayfarer
    private async Task ReUploadBookAsync(Entity<LibraryConsoleComponent> ent, int bookId, string content, EntityUid actor)
    {
        var book = await _dbManager.GetNFLibraryBookByIdAsync(bookId);

        if (book == null)
        {
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            return;
        }
        if (_playerManager.TryGetSessionByEntity(actor, out var session) && session.UserId == book.AuthorPlayerUserId && await _dbManager.UpdateNFBookContentAsync(bookId, content))
        {
            _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
        }
        else
        {
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
        }

        // Refresh UI so the updated book's content field stays in sync.
        UpdateUiState(ent);
    }

    private async Task DeleteBookAsync(Entity<LibraryConsoleComponent> ent, int bookId, EntityUid actor)
    {
        var book = await _dbManager.GetNFLibraryBookByIdAsync(bookId);

        if (book == null)
        {
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            return;
        }
        if (_playerManager.TryGetSessionByEntity(actor, out var session) && session.UserId == book.AuthorPlayerUserId && await _dbManager.DeleteNFLibraryBookAsync(bookId))
        {
            _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
        }
        else
        {
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
        }

        // Refresh UI so the deleted book is remvoed.
        UpdateUiState(ent);
    }
    // End Wayfarer

    private void OnPowerChanged(Entity<LibraryConsoleComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            _ui.CloseUi(ent.Owner, LibraryConsoleUiKey.Key);
    }

    private void UpdateUiState(Entity<LibraryConsoleComponent> ent)
    {
        _ = UpdateUiStateAsync(ent);
    }

    private async Task UpdateUiStateAsync(Entity<LibraryConsoleComponent> ent)
    {
        var books = await _dbManager.GetNFLibraryBooksAsync();

        if (!EntityManager.EntityExists(ent))
            return;

        string? bookContent = null;
        if (ent.Comp.BookSlot.Item is { } bookEntity &&
            TryComp<PaperComponent>(bookEntity, out var paper))
        {
            bookContent = paper.Content;
        }

        var bookData = books
            .Select(book => new LibraryBookData(book.Id, book.Title, book.Author, book.Date, book.Warnings, book.IsNSFW, book.IsPublished, book.AuthorPlayerUserId)) // Wayfarer
            .ToList(); // Wayfarer

        var state = new LibraryConsoleBoundUserInterfaceState(enabled: true, bookContent, bookData);
        _ui.SetUiState(ent.Owner, LibraryConsoleUiKey.Key, state);
    }
}
