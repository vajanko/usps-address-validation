namespace USPS.Tests.Infrastructure;

/// <summary>
/// Response bodies shaped exactly as the USPS Addresses 3.0 specification documents them.
/// </summary>
internal static class TestPayloads
{
    /// <summary>A single exact match (return code 31) for 3120 M St NW, Washington DC.</summary>
    public const string ExactMatch = """
    {
      "firm": "",
      "address": {
        "streetAddress": "3120 M ST NW",
        "streetAddressAbbreviation": "3120 M ST NW",
        "secondaryAddress": "",
        "city": "WASHINGTON",
        "cityAbbreviation": "WASHINGTON",
        "state": "DC",
        "ZIPCode": "20007",
        "ZIPPlus4": "3704",
        "lastline": "WASHINGTON DC 20007-3704",
        "lastlineAbbr": "WASHINGTON DC 20007-3704"
      },
      "additionalInfo": {
        "deliveryPoint": "20",
        "carrierRoute": "C036",
        "DPVConfirmation": "Y",
        "DPVCMRA": "N",
        "business": "Y",
        "DPVBusiness": "Y",
        "centralDeliveryPoint": "N",
        "vacant": "N",
        "DPVVacant": "N",
        "countyName": "DISTRICT OF COLUMBIA",
        "congressDistrict": "AL",
        "recordType": "S",
        "returnCode": 31,
        "returnCodeText": "Single Response - exact match",
        "ZIP5Valid": "Y",
        "POBoxOnlyZIP": "N",
        "electricVehicleRoute": true
      },
      "corrections": [],
      "matches": [
        { "code": "31", "text": "Single Response - exact match" }
      ],
      "warnings": []
    }
    """;

    /// <summary>A default match (return code 32): the building exists but a unit number is missing.</summary>
    public const string DefaultMatch = """
    {
      "address": {
        "streetAddress": "1600 PENNSYLVANIA AVE NW",
        "city": "WASHINGTON",
        "state": "DC",
        "ZIPCode": "20500",
        "ZIPPlus4": "",
        "lastline": "WASHINGTON DC 20500"
      },
      "additionalInfo": {
        "DPVConfirmation": "D",
        "defaultFlag": "Y",
        "returnCode": 32,
        "returnCodeText": "Default address: The address you entered was found but more information is needed (such as an apartment, suite, or box number) to match to a specific address"
      },
      "corrections": [
        {
          "code": "32",
          "text": "Default address: The address you entered was found but more information is needed (such as an apartment, suite, or box number)."
        }
      ],
      "matches": [],
      "warnings": ["The secondary unit designator could not be confirmed."]
    }
    """;

    /// <summary>A multiple-response match (return code 22) returned with HTTP 200.</summary>
    public const string MultipleMatch = """
    {
      "address": {
        "streetAddress": "100 MAIN ST",
        "city": "SPRINGFIELD",
        "state": "IL"
      },
      "additionalInfo": {
        "returnCode": 22,
        "returnCodeText": "Multiple addresses were found for the information you entered, and no default exists."
      },
      "corrections": [
        {
          "code": "22",
          "text": "Multiple addresses were found for the information you entered, and no default exists."
        }
      ],
      "matches": [],
      "warnings": []
    }
    """;

    /// <summary>The USPS 404 envelope for an address that does not exist.</summary>
    public const string AddressNotFoundError = """
    {
      "apiVersion": "v3",
      "error": {
        "code": "404",
        "message": "There is no match for the address requested.",
        "errors": []
      }
    }
    """;

    /// <summary>The USPS 404 envelope for an invalid two-letter state code.</summary>
    public const string InvalidStateError = """
    {
      "apiVersion": "v3",
      "error": {
        "code": "404",
        "message": "The state code in the request is missing or invalid.",
        "errors": []
      }
    }
    """;

    /// <summary>The USPS 404 envelope when more than one address matched.</summary>
    public const string MultipleAddressesError = """
    {
      "apiVersion": "v3",
      "error": {
        "code": "404",
        "message": "More than one address was found matching the requested address.",
        "errors": []
      }
    }
    """;

    /// <summary>A USPS 400 envelope carrying a per-parameter detail.</summary>
    public const string BadRequestError = """
    {
      "apiVersion": "v3",
      "error": {
        "code": "400",
        "message": "The address information in the request is insufficient to match.",
        "errors": [
          {
            "status": "400",
            "code": "1000",
            "title": "Missing parameter",
            "detail": "A ZIP Code or a city and state must be supplied.",
            "source": { "parameter": "ZIPCode", "example": "20007" }
          }
        ]
      }
    }
    """;

    /// <summary>A USPS 401 envelope.</summary>
    public const string UnauthorizedError = """
    {
      "apiVersion": "v3",
      "error": {
        "code": "401",
        "message": "Access token is invalid or expired.",
        "errors": []
      }
    }
    """;

    /// <summary>A USPS 500 envelope.</summary>
    public const string ServerError = """
    {
      "apiVersion": "v3",
      "error": {
        "code": "500",
        "message": "An unexpected error occurred.",
        "errors": []
      }
    }
    """;

    /// <summary>An OAuth error body from the token endpoint.</summary>
    public const string OAuthInvalidClientError = """
    {
      "error": "invalid_client",
      "error_description": "Client credentials are invalid."
    }
    """;
}
