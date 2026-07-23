namespace RoyalNewsDesk.Core.Tests;

/// <summary>A throwaway folder that cleans itself up when the test ends.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "rnd-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Leave the folder behind rather than fail the test run.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
