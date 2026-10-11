using Sciuscia.Foundations;

namespace Sciuscia.Operations;

/// <summary>
/// Raggruppa i servizi del blocco Operazioni (pagamenti, ritiri, messaggi, stampe). Stesso
/// AppPaths e stesso semaforo di manutenzione di Fondazioni, Anagrafiche e Commesse, per restare
/// serializzato con loro.
/// </summary>
public sealed class OperationsRuntime
{
    public OperationsRuntime(AppPaths paths, SemaphoreSlim maintenanceGate)
    {
        Payments = new PaymentService(paths, maintenanceGate);
        Pickups = new PickupService(paths, maintenanceGate);
        Messages = new MessageService(paths, maintenanceGate);
        Prints = new PrintService(paths, maintenanceGate);
    }

    public PaymentService Payments { get; }
    public PickupService Pickups { get; }
    public MessageService Messages { get; }
    public PrintService Prints { get; }
}
