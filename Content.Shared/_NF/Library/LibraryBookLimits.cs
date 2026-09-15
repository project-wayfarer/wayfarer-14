namespace Content.Shared._NF.Library;

/// <summary>
/// Limits for uploaded library books' content, title, and author names.
/// </summary>
public static class LibraryBookLimits
{
    public const int MaxTitleLength = 128;
    public const int MaxAuthorLength = 128;
    public const int MaxContentLength = 32_768;
    public const int MaxContentWarningLength = 1024;  // Wayfarer
}
