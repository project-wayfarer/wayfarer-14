using Robust.Shared.Serialization;

namespace Content.Shared._NF.Library.Events;

/// <summary>
/// Raised by a client requesting to upload a book to the server's library database.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryConsoleUploadBookMessage : BoundUserInterfaceMessage
{
    public string Title;
    public string Author;
    public string Content;
    // Wayfarer
    public string Warnings;
    public bool IsNSFW;
    public bool IsPublished;
    // End Wayfarer

    public LibraryConsoleUploadBookMessage(string title, string author, string content, string warnings, bool isNsfw, bool isPublished) // Wayfarer
    {
        Title = title;
        Author = author;
        Content = content;
        // Wayfarer
        Warnings = warnings;
        IsNSFW = isNsfw;
        IsPublished = isPublished;
        // End Wayfarer
    }
}

/// <summary>
/// Raised by a client requesting to download a library book onto the inserted book entity.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryConsoleDownloadBookMessage : BoundUserInterfaceMessage
{
    public int BookId;

    public LibraryConsoleDownloadBookMessage(int bookId)
    {
        BookId = bookId;
    }
}

/// <summary>
/// Serializable summary of a library book, sent to the client via BUI state.
/// Does not include the full content to keep state transfers lightweight.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryBookData
{
    public int Id;
    public string Title;
    public string Author;
    // Wayfarer
    public DateTime Date;
    public string Warnings;
    public bool IsNSFW;
    public bool IsPublished;
    public Guid? AuthorId;
    // End Wayfarer

    public LibraryBookData(int id, string title, string author, DateTime date, string warnings, bool isNSFW, bool isPublished, Guid? authorId) // Wayfarer
    {
        Id = id;
        Title = title;
        Author = author;
        Date = date;
        // Wayfarer
        Warnings = warnings;
        IsNSFW = isNSFW;
        IsPublished = isPublished;
        AuthorId = authorId;
        // End Wayfarer
    }
}

// Wayfarer
/// <summary>
/// Raised by a client requesting to upload a book to the server's library database.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryConsoleReUploadBookMessage : BoundUserInterfaceMessage
{
    public int Id;
    public string Content;

    public LibraryConsoleReUploadBookMessage(int id, string content) // Wayfarer
    {
        Id = id;
        Content = content;
    }
}

/// <summary>
/// Raised by a client requesting to upload a book to the server's library database.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryConsoleDeleteOwnedBookMessage : BoundUserInterfaceMessage
{
    public int Id;

    public LibraryConsoleDeleteOwnedBookMessage(int id) // Wayfarer
    {
        Id = id;
    }
}
// End Wayfarer
