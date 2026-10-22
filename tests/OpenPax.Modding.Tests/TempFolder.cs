namespace OpenPax.Modding.Tests;

/// <summary>A scratch folder for one test, removed after it.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openpax-mods-" + Guid.NewGuid().ToString("N"));

    public string Write(string relative, string text)
    {
        var file = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file) ?? Path);
        File.WriteAllText(file, text);
        return file;
    }

    public void Dispose() => Directory.Delete(Path, true);
}
