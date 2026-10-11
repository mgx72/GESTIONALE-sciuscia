using Sciuscia.Foundations;
using Sciuscia.Operations;
using Sciuscia.Orders;
using Sciuscia.Registry;

var selfTest = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);
if (selfTest)
{
    await FoundationSelfTest.RunAsync();
    await RegistrySelfTest.RunAsync();
    await OrdersSelfTest.RunAsync();
    await OperationsSelfTest.RunAsync();
    return;
}

var dataRoot = ReadOption(args, "--data-root") ?? AppPaths.DefaultDataRoot();
var runtime = new FoundationRuntime(new FoundationOptions(dataRoot));

try
{
    var installation = await runtime.Database.EnsureCreatedAsync();
    Console.WriteLine("Gestionale Sciuscià — Fondazioni Solo LAB");
    Console.WriteLine("Stato: operativo");
    Console.WriteLine($"Database: {installation.DatabasePath}");
    Console.WriteLine($"Fotografie: {runtime.Paths.PhotosRoot}");
    Console.WriteLine($"Backup: {runtime.Paths.BackupRoot}");
    Console.WriteLine("Backup automatico previsto: 14:00");
}
catch (Exception exception)
{
    await runtime.Errors.WriteAsync(exception, "Avvio Fondazioni");
    Console.Error.WriteLine("Avvio non riuscito. Il dettaglio è stato registrato nel file errori.");
    Environment.ExitCode = 1;
}

static string? ReadOption(string[] arguments, string option)
{
    for (var index = 0; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
        {
            return arguments[index + 1];
        }
    }

    return null;
}

