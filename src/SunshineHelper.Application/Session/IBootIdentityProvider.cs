using SunshineHelper.Domain.Session;

namespace SunshineHelper.Application.Session;

/// <summary>Supplies the current boot identity, which
/// <see cref="RecoveryDecider"/> uses to tell a crashed app apart from a restarted PC.</summary>
public interface IBootIdentityProvider
{
    /// <summary>Null when the boot identity cannot be determined. That is treated as
    /// "same boot", so recovery restores everything rather than risk leaving the PC in
    /// streaming configuration.</summary>
    BootIdentity? GetCurrent();
}
