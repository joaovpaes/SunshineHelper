namespace SunshineHelper.Domain.Session;

/// <summary>The session snapshot on disk could not be parsed, or was written by a
/// version of the app whose format this one does not know. The snapshot is kept rather
/// than deleted: it is the only record of how to put the environment back, and guessing
/// at a format would restore the wrong state.</summary>
public sealed class SnapshotFormatException : Exception
{
    public SnapshotFormatException()
    {
    }

    public SnapshotFormatException(string message)
        : base(message)
    {
    }

    public SnapshotFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
