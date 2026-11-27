using System.Text;

namespace Depot.Documents;

/// <summary>
/// A document open in Depot: a UIScript file it edits, or a Maxis <c>.uis</c> file it only shows. Saving writes the
/// text exactly as edited, with the byte-order mark the file had, if any.
/// </summary>
public sealed class DocumentFile
{
    /// <summary>The largest document Depot opens, the same as depot-render draws.</summary>
    public const int MaximumSize = 4 * 1024 * 1024;

    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private DocumentFile(string path, string text, bool hasByteOrderMark)
    {
        Path = path;
        Text = text;
        HasByteOrderMark = hasByteOrderMark;
    }

    /// <summary>The file, as a full path.</summary>
    public string Path { get; }

    /// <summary>The text as it was opened or last saved, without a byte-order mark.</summary>
    public string Text { get; private set; }

    /// <summary>Whether the file starts with a byte-order mark, which saving keeps.</summary>
    public bool HasByteOrderMark { get; }

    /// <summary>Whether it's a <c>.uis</c> file, which Depot shows and never writes.</summary>
    public bool IsUis => string.Equals(System.IO.Path.GetExtension(Path), ".uis", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether Depot can save it.</summary>
    public bool IsReadOnly => IsUis;

    /// <summary>The file's name, for titles.</summary>
    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>Opens a document.</summary>
    /// <exception cref="DocumentException">It can't be read, is too large, or a UIScript file isn't UTF-8.</exception>
    public static DocumentFile Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = System.IO.Path.GetFullPath(path);
        byte[] bytes;
        try
        {
            FileInfo file = new(full);
            if (!file.Exists)
                throw new DocumentException(Words.Get(TextKeys.DocumentMissing, full));
            if (file.Length > MaximumSize)
                throw new DocumentException(Words.Get(TextKeys.DocumentTooLarge, file.Name, $"{file.Length:N0}", $"{MaximumSize:N0}"));
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DocumentException(Words.Get(TextKeys.DocumentUnreadable, full, exception.Message), exception);
        }

        var hasMark = bytes.AsSpan().StartsWith(ByteOrderMark);
        var body = hasMark ? bytes.AsSpan(ByteOrderMark.Length) : bytes.AsSpan();
        string text;
        try
        {
            text = StrictUtf8.GetString(body);
        }
        catch (DecoderFallbackException exception)
        {
            // UIScript is UTF-8, so text that isn't is refused rather than garbled. A .uis file is only shown, and
            // depot-render reads it from disk, so its text is shown as well as it can be.
            if (!string.Equals(System.IO.Path.GetExtension(full), ".uis", StringComparison.OrdinalIgnoreCase))
                throw new DocumentException(Words.Get(TextKeys.DocumentNotUtf8, System.IO.Path.GetFileName(full)), exception);
            text = Encoding.Latin1.GetString(body);
        }

        return new DocumentFile(full, text, hasMark);
    }

    /// <summary>Writes <paramref name="text"/> to the file, replacing it only once all of it is written.</summary>
    /// <exception cref="InvalidOperationException">The document is read-only.</exception>
    /// <exception cref="DocumentException">The file couldn't be written; it's left as it was.</exception>
    public void Save(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsReadOnly)
            throw new InvalidOperationException($"{Name} is a .uis file, which Depot doesn't change.");

        var folder = System.IO.Path.GetDirectoryName(Path)!;
        var temporary = System.IO.Path.Combine(folder, $".{Name}.{Guid.NewGuid():N}.saving");
        try
        {
            using (var stream = File.Create(temporary))
            {
                if (HasByteOrderMark)
                    stream.Write(ByteOrderMark);
                stream.Write(StrictUtf8.GetBytes(text));
            }

            File.Move(temporary, Path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or EncoderFallbackException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The save already failed and says so; a stray temporary file is the lesser problem.
            }

            throw new DocumentException(Words.Get(TextKeys.DocumentUnsaved, Name, exception.Message), exception);
        }

        Text = text;
    }
}

/// <summary>A document couldn't be opened or saved.</summary>
public sealed class DocumentException : Exception
{
    public DocumentException(string message) : base(message) { }

    public DocumentException(string message, Exception inner) : base(message, inner) { }
}
