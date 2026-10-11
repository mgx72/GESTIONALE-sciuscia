namespace Sciuscia.Foundations;

public sealed record FoundationOptions(
    string DataRoot,
    string SiteCode = "LAB",
    string SiteName = "Laboratorio",
    string StoreName = "Sciuscià",
    string? StoreAddress = null,
    string? StorePhone1 = null,
    string? StorePhone2 = null,
    string? OpeningHours = null)
{
    public const string ApprovedReceiptWarning =
        "La presente ricevuta deve essere esibita al momento del ritiro. " +
        "Il bene dovrà essere ritirato dal cliente presso il nostro negozio entro sei mesi " +
        "dalla comunicazione di avvenuta ultimazione della lavorazione. " +
        "Decorso tale termine, non potrà essere garantita la conservazione del bene nelle medesime condizioni.";
}

