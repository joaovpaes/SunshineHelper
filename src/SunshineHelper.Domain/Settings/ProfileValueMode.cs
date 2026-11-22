namespace SunshineHelper.Domain.Settings;

/// <summary>
/// How a profile treats one setting when applying an environment: follow what the
/// connecting client asked for, force a fixed value, or leave the current value
/// untouched. See docs/adr/0002-arquitetura-do-nucleo.md, "Escolha do perfil".
/// </summary>
public enum ProfileValueMode
{
    FollowClient,
    Fixed,
    Leave,
}
