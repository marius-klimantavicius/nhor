using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter;

/// <summary>Local filesystem enumeration/stat only. Never creates folders or writes files.
/// Optional root is a navigation boundary for fixtures/application scopes, not a security sandbox.</summary>
public sealed class LocalFilePickerProvider : IFilePickerProvider
{
    private readonly string? _root;
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    public LocalFilePickerProvider(string? rootDirectory = null) => _root = rootDirectory == null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
    public PickerPath Normalize(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (_root != null && !full.Equals(_root, Comparison) && !full.StartsWith(Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar, Comparison))
            throw new IOException("Choose a location inside " + _root + ".");
        return new(full);
    }
    public PickerPath? Parent(PickerPath path)
    {
        var full = Normalize(path.Value);
        if (_root != null && full.Value.Equals(_root, Comparison)) return null;
        return Directory.GetParent(full.Value) is { } parent ? Normalize(parent.FullName) : null;
    }
    public PickerPath Child(PickerPath directory, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar) || Path.IsPathRooted(name))
            throw new IOException("Enter one file name without directory separators.");
        return Normalize(Path.Combine(Normalize(directory.Value).Value, name));
    }
    public Task<IReadOnlyList<FilePickerEntry>> ListAsync(PickerPath directory, CancellationToken cancellationToken)
    {
        var path = Normalize(directory.Value);
        return Task.Run<IReadOnlyList<FilePickerEntry>>(() =>
        {
            var entries = new List<FilePickerEntry>();
            foreach (var item in new DirectoryInfo(path.Value).EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(new(Normalize(item.FullName), item.Name, (item.Attributes & FileAttributes.Directory) != 0));
            }
            cancellationToken.ThrowIfCancellationRequested();
            return entries.AsReadOnly();
        }, cancellationToken);
    }
    public Task<FilePickerEntry?> GetEntryAsync(PickerPath path, CancellationToken cancellationToken)
    {
        path = Normalize(path.Value);
        return Task.Run<FilePickerEntry?>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new(path, Path.GetFileName(path.Value), (File.GetAttributes(path.Value) & FileAttributes.Directory) != 0); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }, cancellationToken);
    }
}
