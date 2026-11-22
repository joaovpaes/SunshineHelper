namespace SunshineHelper.Domain.Settings;

/// <summary>settings.json could not be parsed, migrated or interpreted as
/// <see cref="AppSettings"/>. Distinct from <see cref="SettingsValidationResult"/>,
/// which reports structural problems in an otherwise well-formed document.</summary>
public sealed class SettingsFormatException : Exception
{
    public SettingsFormatException()
    {
    }

    public SettingsFormatException(string message)
        : base(message)
    {
    }

    public SettingsFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
