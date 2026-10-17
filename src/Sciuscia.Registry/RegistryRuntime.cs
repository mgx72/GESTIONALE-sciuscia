using Sciuscia.Foundations;

namespace Sciuscia.Registry;

/// <summary>
/// Raggruppa i servizi del blocco Anagrafiche. Riceve lo stesso AppPaths e lo stesso semaforo
/// di manutenzione delle Fondazioni, in modo che tutte le scritture sul database (Fondazioni e
/// Anagrafiche insieme) restino serializzate all'interno del singolo processo, come già avviene
/// tra Database/Numbering/Photos/Backups in FoundationRuntime.
/// </summary>
public sealed class RegistryRuntime
{
    public RegistryRuntime(AppPaths paths, SemaphoreSlim maintenanceGate)
    {
        Customers = new CustomerService(paths, maintenanceGate);
        Categories = new CategoryService(paths, maintenanceGate);
        Brands = new BrandService(paths, maintenanceGate);
        Artisans = new ArtisanService(paths, maintenanceGate);
        WorkTypes = new WorkTypeService(paths, maintenanceGate);
        ListinoImport = new ListinoImportService(paths, maintenanceGate);
    }

    public CustomerService Customers { get; }
    public CategoryService Categories { get; }
    public BrandService Brands { get; }
    public ArtisanService Artisans { get; }
    public WorkTypeService WorkTypes { get; }
    public ListinoImportService ListinoImport { get; }
}
