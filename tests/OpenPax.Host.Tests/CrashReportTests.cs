namespace OpenPax.Host.Tests;

/// <summary>A crash leaves a log saying what failed, even where there is no console to print to (Windows).</summary>
public sealed class CrashReportTests
{
    [Fact]
    public void ACrashWritesItsLog()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"openpax-crash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            CrashReport.ResetForTests(folder);
            Exception error;
            try
            {
                throw new InvalidOperationException("the import fell over");
            }
            catch (InvalidOperationException e)
            {
                error = e;
            }

            var written = CrashReport.Report(error, showBox: false);
            Assert.Equal(Path.Combine(folder, CrashReport.CrashFile), written);
            var text = File.ReadAllText(written!);
            Assert.Contains("the import fell over", text, StringComparison.Ordinal);
            Assert.Contains(nameof(ACrashWritesItsLog), text, StringComparison.Ordinal); // the stack trace
            Assert.Contains("OpenPax ", text, StringComparison.Ordinal);

            // Only the first crash of a run is reported.
            Assert.Null(CrashReport.Report(new InvalidOperationException("again"), showBox: false));
        }
        finally
        {
            CrashReport.ResetForTests(null);
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void TheTeeWritesBothAndOutlivesTheSecond()
    {
        var first = new StringWriter();
        var second = new StringWriter();
        var tee = new TeeWriter(first, second);
        tee.WriteLine("one");
        second.Dispose();
        tee.WriteLine("two");
        Assert.Equal($"one{Environment.NewLine}two{Environment.NewLine}", first.ToString());
    }
}
