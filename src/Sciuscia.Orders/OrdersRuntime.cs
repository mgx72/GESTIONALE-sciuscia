using Sciuscia.Foundations;

namespace Sciuscia.Orders;

/// <summary>
/// Raggruppa i servizi del blocco Commesse (ordini e articoli). Stesso AppPaths e stesso
/// semaforo di manutenzione di Fondazioni e Anagrafiche, per restare serializzato con loro.
/// </summary>
public sealed class OrdersRuntime
{
    public OrdersRuntime(AppPaths paths, SemaphoreSlim maintenanceGate, PhotoStorageService photoStorage)
    {
        Orders = new OrderService(paths, maintenanceGate);
        Items = new ItemService(paths, maintenanceGate);
        ItemWorks = new ItemWorkService(paths, maintenanceGate);
        ExternalAssignments = new ExternalAssignmentService(paths, maintenanceGate);
        Photos = new PhotoService(paths, maintenanceGate, photoStorage, Orders, Items);
        Agenda = new AgendaService(paths);
        Home = new HomeService(paths);
        ReadyPickups = new ReadyPickupService(paths);
        ArtisanWork = new ArtisanWorkService(paths);
    }

    public OrderService Orders { get; }
    public ItemService Items { get; }
    public ItemWorkService ItemWorks { get; }
    public ExternalAssignmentService ExternalAssignments { get; }
    public PhotoService Photos { get; }
    public AgendaService Agenda { get; }
    public HomeService Home { get; }
    public ReadyPickupService ReadyPickups { get; }
    public ArtisanWorkService ArtisanWork { get; }
}
