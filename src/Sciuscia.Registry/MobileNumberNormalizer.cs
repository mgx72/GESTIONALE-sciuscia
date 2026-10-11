using System.Text;

namespace Sciuscia.Registry;

/// <summary>
/// Normalizza un numero di cellulare italiano per il controllo di univocità
/// (regola 1 dello schema: "Il cellulare normalizzato non può appartenere a due clienti diversi").
/// Il numero originale digitato dall'operatore resta comunque conservato senza modifiche.
/// </summary>
public static class MobileNumberNormalizer
{
    /// <summary>
    /// Restituisce il numero normalizzato (solo cifre, senza prefisso internazionale),
    /// oppure null se il valore in ingresso è vuoto: un cellulare non indicato non è un duplicato.
    /// </summary>
    public static string? Normalize(string? mobileOriginal)
    {
        if (string.IsNullOrWhiteSpace(mobileOriginal))
        {
            return null;
        }

        var digitsOnly = new StringBuilder();
        foreach (var character in mobileOriginal)
        {
            if (char.IsDigit(character))
            {
                digitsOnly.Append(character);
            }
        }

        var digits = digitsOnly.ToString();

        if (digits.StartsWith("0039", StringComparison.Ordinal))
        {
            digits = digits[4..];
        }
        else if (digits.StartsWith("39", StringComparison.Ordinal) && digits.Length > 10)
        {
            digits = digits[2..];
        }

        if (digits.Length == 0)
        {
            return null;
        }

        if (digits.Length < 6 || digits.Length > 15)
        {
            throw new ArgumentException(
                "Numero di cellulare non valido: controllare le cifre inserite.",
                nameof(mobileOriginal));
        }

        return digits;
    }
}
