namespace OpenPax.Host.Update;

/// <summary>A download's 0 to 1 as whole percents, each told once.</summary>
internal sealed class DownloadProgress(Action<int> percentDone) : IProgress<double>
{
    private int _percent = -1;

    public void Report(double value)
    {
        var percent = Math.Clamp((int)(value * 100), 0, 100);
        if (percent != _percent)
        {
            _percent = percent;
            percentDone(percent);
        }
    }
}
