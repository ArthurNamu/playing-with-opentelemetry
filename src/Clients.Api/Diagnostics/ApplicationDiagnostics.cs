using System.Diagnostics.Metrics;

namespace Clients.Api.Diagnostics;

public static class ApplicationDiagnostics
{
    private const string ServiceName = "Clients.Api";
    public static readonly Meter Meter = new Meter(ServiceName);

    public static Counter<long> ClientsCreatedCounter = Meter.CreateCounter<long>("clients.created");


}
