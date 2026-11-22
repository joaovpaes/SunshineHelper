// "Application" is qualified below because this project transitively references
// SunshineHelper.Application (via Infrastructure), and C# resolves an unqualified
// name against an enclosing namespace (here, a sibling "SunshineHelper.Application"
// namespace) before it considers types brought in by a `using` directive.
namespace SunshineHelper.App;

public partial class App : System.Windows.Application
{
}
