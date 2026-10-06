using USPS.AddressValidation;
using USPS.AddressValidation.DependencyInjection;
using USPS.AddressValidation.Exceptions;
using USPS.AddressValidation.Models;
using USPS.SampleApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


// ---------------------------------------------------------------------------------------------
// USPS sample: validates a set of US addresses against the USPS Addresses 3.0 API and prints
// what came back, including the fields USPS standardized or completed.
//
//   dotnet run --project samples/USPS.SampleApp
//   dotnet run --project samples/USPS.SampleApp -- --street "3120 M St NW" --city Washington --state DC
//
// Credentials come from user secrets, environment variables (Usps__ClientId / Usps__ClientSecret)
// or command-line switches (--Usps:ClientId ...).
// ---------------------------------------------------------------------------------------------

var switchMappings = new Dictionary<string, string>
{
    ["--street"] = "Address:StreetAddress",
    ["--secondary"] = "Address:SecondaryAddress",
    ["--city"] = "Address:City",
    ["--state"] = "Address:State",
    ["--zip"] = "Address:ZipCode",   // 20007 or 20007-3704
    ["--firm"] = "Address:Firm",
};

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets(typeof(ResultPrinter).Assembly, optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args, switchMappings)
    .Build();

var services = new ServiceCollection();
services.AddUspsAddressValidation(configuration.GetSection(UspsOptions.SectionName));
await using var provider = services.BuildServiceProvider();

IUspsAddressValidator validator;
try
{
    // Missing or blank credentials surface here, as a UspsConfigurationException.
    validator = provider.GetRequiredService<IUspsAddressValidator>();
}
catch (UspsConfigurationException exception)
{
    ResultPrinter.WriteLine($"Configuration problem: {exception.Message}", ConsoleColor.Red);
    Console.WriteLine();
    Console.WriteLine("Supply your USPS Consumer Key and Consumer Secret in one of these ways:");
    Console.WriteLine();
    Console.WriteLine("  dotnet user-secrets set \"Usps:ClientId\" \"<consumer key>\" --project samples/USPS.SampleApp");
    Console.WriteLine("  dotnet user-secrets set \"Usps:ClientSecret\" \"<consumer secret>\" --project samples/USPS.SampleApp");
    Console.WriteLine();
    Console.WriteLine("  setx Usps__ClientId \"<consumer key>\"          (Windows, new shells only)");
    Console.WriteLine("  export Usps__ClientId=\"<consumer key>\"        (bash)");
    Console.WriteLine();
    Console.WriteLine("  dotnet run -- --Usps:ClientId <consumer key> --Usps:ClientSecret <consumer secret>");
    return 2;
}

var requested = BuildRequestedAddress(configuration.GetSection("Address"));
var addresses = requested is not null
    ? [("Address supplied on the command line", requested)]
    : DemoAddresses();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var exitCode = 0;

foreach (var (title, address) in addresses)
{
    ResultPrinter.PrintHeader(title);

    try
    {
        var result = await validator.ValidateAsync(address, cancellation.Token);
        ResultPrinter.Print(address, result);
    }
    catch (UspsConfigurationException exception)
    {
        // A missing API key or an unusable endpoint: nothing to retry, stop here.
        ResultPrinter.WriteLine($"  Configuration error: {exception.Message}", ConsoleColor.Red);
        return 2;
    }
    catch (UspsAuthenticationException exception)
    {
        ResultPrinter.WriteLine($"  Authentication failed ({(int)exception.StatusCode}): {exception.Message}",
            ConsoleColor.Red);
        return 3;
    }
    catch (UspsRateLimitException exception)
    {
        var wait = exception.RetryAfter is { } retryAfter ? $" Retry after {retryAfter.TotalSeconds:0}s." : string.Empty;
        ResultPrinter.WriteLine($"  Throttled by USPS.{wait}", ConsoleColor.Red);
        exitCode = 4;
    }
    catch (UspsTransportException exception)
    {
        ResultPrinter.WriteLine($"  USPS could not be reached: {exception.Message}", ConsoleColor.Red);
        exitCode = 5;
    }
    catch (UspsException exception)
    {
        // The catch-all for every other USPS failure: server faults, unparseable responses.
        ResultPrinter.WriteLine($"  Request failed: {exception.Message}", ConsoleColor.Red);
        exitCode = 6;
    }
}

// Show what happens when the service is unreachable, without needing to unplug anything.
if (requested is null)
{
    ResultPrinter.PrintHeader("Unreachable service (deliberately pointed at a bad host)");
    var brokenOptions = new UspsOptions
    {
        ClientId = configuration["Usps:ClientId"],
        ClientSecret = configuration["Usps:ClientSecret"],
        BaseAddress = new Uri("https://apis.usps.invalid/"),
        Timeout = TimeSpan.FromSeconds(10),
    };

    try
    {
        var broken = UspsAddressValidator.Create(brokenOptions);
        await broken.ValidateAsync(
            AddressInput.Create("3120 M St NW", city: "Washington", state: "DC"),
            cancellation.Token);

        ResultPrinter.WriteLine("  Unexpectedly succeeded.", ConsoleColor.Magenta);
    }
    catch (UspsTransportException exception)
    {
        ResultPrinter.WriteLine($"  Threw {exception.GetType().Name}: {exception.Message}", ConsoleColor.Green);
    }
}

Console.WriteLine();
return exitCode;

static AddressInput? BuildRequestedAddress(IConfigurationSection section)
{
    var street = section["StreetAddress"];
    if (string.IsNullOrWhiteSpace(street))
    {
        return null;
    }

    return new AddressInput
    {
        Firm = section["Firm"],
        StreetAddress = street,
        SecondaryAddress = section["SecondaryAddress"],
        City = section["City"],
        State = section["State"],
        ZipCode = section["ZipCode"],
    };
}

static (string Title, AddressInput Address)[] DemoAddresses() =>
[
    ("1. A complete address — expect an exact match",
        AddressInput.Create("3120 M St NW", city: "Washington", state: "DC", zipCode: "20007")),

    ("2. No ZIP Code — USPS completes it, including ZIP+4",
        AddressInput.Create("3120 M St NW", city: "Washington", state: "DC")),

    ("3. Unabbreviated and lower case — USPS standardizes it",
        AddressInput.Create("475 lenfant plaza southwest", city: "washington", state: "dc")),

    ("4. ZIP Code only, no city or state — USPS fills in the last line",
        AddressInput.Create("3120 M St NW", zipCode: "20007")),

    ("5. Apartment building with no unit number — expect a default match",
        AddressInput.Create("1 Rockefeller Plz", city: "New York", state: "NY")),

    ("6. A street that does not exist — expect USPS to reject it",
        AddressInput.Create("99999 Nonexistent Pkwy", city: "Washington", state: "DC")),

    ("7. Real street in the wrong ZIP Code — expect USPS to reject it",
        AddressInput.Create("3120 M St NW", zipCode: "10018")),

    ("8. Invalid state code — rejected locally, no call is made",
        AddressInput.Create("3120 M St NW", city: "Washington", state: "ZZ")),

    ("9. No city, state or ZIP Code — rejected locally, no call is made",
        AddressInput.Create("3120 M St NW")),
];
