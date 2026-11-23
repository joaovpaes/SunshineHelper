using SunshineHelper.Application.Monitoring;

namespace SunshineHelper.Application.Tests.Monitoring;

/// <summary>Says which Sunshine process ids are "running"; counts how often it was asked.</summary>
internal sealed class FakeSunshineProcessProbe : ISunshineProcessProbe
{
    public List<int> Running { get; } = [];

    public int Calls { get; private set; }

    public Exception? Throws { get; set; }

    public IReadOnlyCollection<int> FindProcessIds()
    {
        Calls++;
        if (Throws is not null)
        {
            throw Throws;
        }

        return [.. Running];
    }
}
