using USPS.AddressValidation.Models;

namespace USPS.SampleApp;

/// <summary>
/// Renders an <see cref="AddressValidationResult"/> for the console.
/// </summary>
internal static class ResultPrinter
{
    public static void PrintHeader(string title)
    {
        Console.WriteLine();
        WriteLine(new string('─', 72), ConsoleColor.DarkGray);
        WriteLine(title, ConsoleColor.White);
        WriteLine(new string('─', 72), ConsoleColor.DarkGray);
    }

    public static void Print(AddressInput input, AddressValidationResult result)
    {
        Console.WriteLine($"  Submitted : {input}");

        var (label, color) = Describe(result.Status);
        Console.Write("  Outcome   : ");
        WriteLine($"{result.Status} — {label}", color);

        if (result.Address is not null)
        {
            Console.WriteLine("  Standardized:");
            foreach (var line in result.Address.ToAddressLines())
            {
                WriteLine($"      {line}", ConsoleColor.Cyan);
            }
        }

        if (result.Changes.Count > 0)
        {
            Console.WriteLine("  USPS changed:");
            foreach (var change in result.Changes)
            {
                var verb = change.Kind == AddressChangeKind.Completed ? "completed" : "corrected";
                WriteLine($"      {change.Field} {verb}: {Show(change.Original)} -> {Show(change.Standardized)}",
                    ConsoleColor.Yellow);
            }
        }

        if (result.Errors.Count > 0)
        {
            Console.WriteLine("  Problems:");
            foreach (var error in result.Errors)
            {
                var origin = error.Source == ValidationErrorSource.Local ? "local" : "USPS";
                var field = error.Field is null ? string.Empty : $" [{error.Field}]";
                WriteLine($"      ({origin}){field} {error.Message}", ConsoleColor.Red);
            }
        }

        foreach (var correction in result.Corrections)
        {
            WriteLine($"  Correction: {correction}", ConsoleColor.Yellow);
        }

        foreach (var warning in result.Warnings)
        {
            WriteLine($"  Warning   : {warning}", ConsoleColor.Yellow);
        }

        if (result.AdditionalInfo is { } info)
        {
            var facts = new List<string>();
            if (!string.IsNullOrWhiteSpace(info.DpvConfirmation)) facts.Add($"DPV={info.DpvConfirmation}");
            if (!string.IsNullOrWhiteSpace(info.CarrierRoute)) facts.Add($"route={info.CarrierRoute}");
            if (!string.IsNullOrWhiteSpace(info.CountyName)) facts.Add($"county={info.CountyName}");
            if (info.IsBusiness) facts.Add("business");
            if (info.IsVacant) facts.Add("vacant");

            if (facts.Count > 0)
            {
                WriteLine($"  Delivery  : {string.Join(", ", facts)}", ConsoleColor.DarkGray);
            }
        }
    }

    private static (string Label, ConsoleColor Color) Describe(AddressValidationStatus status) => status switch
    {
        AddressValidationStatus.Validated =>
            ("the address matches exactly one USPS delivery point", ConsoleColor.Green),
        AddressValidationStatus.DefaultAddress =>
            ("the building exists but a unit number is needed", ConsoleColor.Yellow),
        AddressValidationStatus.MultipleMatches =>
            ("several addresses match; more detail is needed", ConsoleColor.Yellow),
        AddressValidationStatus.InvalidInput =>
            ("rejected locally, no call was made to USPS", ConsoleColor.Magenta),
        AddressValidationStatus.Unknown =>
            ("USPS answered but the outcome could not be classified", ConsoleColor.Magenta),
        _ => ("USPS could not validate this address", ConsoleColor.Red),
    };

    private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "(empty)" : $"'{value}'";

    public static void WriteLine(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }
}
