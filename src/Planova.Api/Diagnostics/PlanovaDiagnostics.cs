using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Planova.Api.Diagnostics;

public static class PlanovaDiagnostics
{
    public const string Name = "Planova";
    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name, "1.0.0");
    public static readonly Counter<long> AuthorizationDenied = Meter.CreateCounter<long>("planova.authorization.denied");
    public static readonly Counter<long> FoundationContextViews = Meter.CreateCounter<long>("planova.foundation.context.views");
}
