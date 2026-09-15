using Content.Client._NF.Library.UI;
using Content.Shared._NF.Library.BUI;
using Content.Shared._NF.Library.Events;
using Robust.Client.UserInterface;

namespace Content.Client._NF.Library.BUI;

public sealed class LibraryConsoleBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private LibraryConsoleMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<LibraryConsoleMenu>();
        _menu.UploadBookRequested += OnUploadBook;
        _menu.DownloadBookRequested += OnDownloadBook;
        _menu.ReUploadBookRequested += OnReUploadBook; // Wayfarer
        _menu.DeleteBookRequested += OnDeleteBook; // Wayfarer
    }

    // Wayfarer
    private void OnDeleteBook(int id)
    {
        SendMessage(new LibraryConsoleDeleteOwnedBookMessage(id));
    }

    private void OnReUploadBook(int id, string content)
    {
        SendMessage(new LibraryConsoleReUploadBookMessage(id, content));
    }
    // End Wayfarer

    private void OnUploadBook(string title, string author, string content, string warnings, bool isNsfw, bool published) // Wayfarer
    {
        SendMessage(new LibraryConsoleUploadBookMessage(title, author, content, warnings, isNsfw, published)); // Wayfarer
    }

    private void OnDownloadBook(int bookId)
    {
        SendMessage(new LibraryConsoleDownloadBookMessage(bookId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not LibraryConsoleBoundUserInterfaceState libraryState)
            return;

        _menu?.SetEnabled(libraryState.Enabled);
        _menu?.SetBookContent(libraryState.Enabled, libraryState.BookContent);
        _menu?.PopulateBookList(libraryState.Books);
    }
}
