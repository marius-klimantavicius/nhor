using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>Deterministic borrowed provider shared by both Files modules. No disk writes.</summary>
internal sealed class FilesFixtureProvider : IFilePickerProvider
{
    public bool SlowNext { get; set; }
    public bool FailNext { get; set; }
    private static readonly FilePickerEntry[] Entries =
    [
        new(new("/Files/Documents"), "Documents", true), new(new("/Files/Empty"), "Empty", true),
        new(new("/Files/cover.svg"), "cover.svg", false), new(new("/Files/motion.json"), "motion.json", false),
        new(new("/Files/notes.txt"), "notes.txt", false), new(new("/Files/Žąsis 👩🏽‍💻é.svg"), "Žąsis 👩🏽‍💻é.svg", false),
        new(new("/Files/Documents/review.svg"), "review.svg", false)
    ];
    public PickerPath Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        if (full != "/Files" && !full.StartsWith("/Files/", StringComparison.Ordinal)) throw new IOException("This sample uses the /Files fixture only.");
        return new(full);
    }
    public PickerPath? Parent(PickerPath path) => path.Value == "/Files" ? null : Normalize(Path.GetDirectoryName(path.Value)!);
    public PickerPath Child(PickerPath directory, string name)
    {
        if (name.Contains('/') || name is "." or "..") throw new IOException("Enter one file name.");
        return Normalize(directory.Value + "/" + name);
    }
    public async Task<IReadOnlyList<FilePickerEntry>> ListAsync(PickerPath directory, CancellationToken token)
    {
        bool slow = SlowNext, fail = FailNext; SlowNext = FailNext = false;
        await Task.Delay(slow ? 3000 : 80, token).ConfigureAwait(false);
        if (fail) throw new IOException("Fixture folder temporarily unavailable. Retry listing to continue.");
        return Entries.Where(i => Path.GetDirectoryName(i.Path.Value) == directory.Value).ToArray();
    }
    public Task<FilePickerEntry?> GetEntryAsync(PickerPath path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(path.Value == "/Files" ? new FilePickerEntry(path, "Files", true) : Entries.FirstOrDefault(i => i.Path == path));
    }
}
