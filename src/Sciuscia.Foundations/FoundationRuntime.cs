using Sciuscia.Operations;
using Sciuscia.Orders;
using Sciuscia.Registry;

namespace Sciuscia.Foundations;

public sealed class FoundationRuntime
{
    private readonly SemaphoreSlim _maintenanceGate = new(1, 1);

    public FoundationRuntime(FoundationOptions options)
    {
        Options = options;
        Paths = new AppPaths(options.DataRoot);
        Errors = new ErrorLog(Paths);
        Database = new DatabaseInitializer(Paths, Options, _maintenanceGate);
        Numbering = new NumberingService(Paths, _maintenanceGate);
        Photos = new PhotoStorageService(Paths, _maintenanceGate);
        Backups = new BackupService(Paths, _maintenanceGate);
        // Stesso AppPaths e stesso semaforo delle Fondazioni: le scritture di Anagrafiche e
        // Commesse restano serializzate insieme a quelle di Fondazioni in un solo processo.
        Registry = new RegistryRuntime(Paths, _maintenanceGate);
        Orders = new OrdersRuntime(Paths, _maintenanceGate);
        Operations = new OperationsRuntime(Paths, _maintenanceGate);
    }

    public FoundationOptions Options { get; }
    public AppPaths Paths { get; }
    public ErrorLog Errors { get; }
    public DatabaseInitializer Database { get; }
    public NumberingService Numbering { get; }
    public PhotoStorageService Photos { get; }
    public BackupService Backups { get; }
    public RegistryRuntime Registry { get; }
    public OrdersRuntime Orders { get; }
    public OperationsRuntime Operations { get; }
}

