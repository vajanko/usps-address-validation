using USPS.AddressValidation.Internal;
using USPS.AddressValidation.Models;

namespace USPS.Tests.Unit;

/// <summary>
/// Mapping of the several ways USPS signals an outcome onto one status.
/// </summary>
public sealed class UspsStatusMapperTests
{
    [Theory]
    [InlineData(31, AddressValidationStatus.Validated)]
    [InlineData(32, AddressValidationStatus.DefaultAddress)]
    [InlineData(22, AddressValidationStatus.MultipleMatches)]
    [InlineData(21, AddressValidationStatus.AddressNotFound)]
    [InlineData(13, AddressValidationStatus.InvalidCity)]
    [InlineData(12, AddressValidationStatus.InvalidState)]
    [InlineData(11, AddressValidationStatus.InvalidZipCode)]
    [InlineData(10, AddressValidationStatus.InvalidAddress)]
    [InlineData(99, AddressValidationStatus.Unknown)]
    public void Maps_usps_return_codes(int returnCode, AddressValidationStatus expected)
    {
        Assert.Equal(expected, UspsStatusMapper.FromReturnCode(returnCode));
    }

    [Theory]
    [InlineData("There is no match for the address requested.", AddressValidationStatus.AddressNotFound)]
    [InlineData("The address requested could not be found.", AddressValidationStatus.AddressNotFound)]
    [InlineData("The city in the request is missing or invalid.", AddressValidationStatus.InvalidCity)]
    [InlineData("The state code in the request is missing or invalid.", AddressValidationStatus.InvalidState)]
    [InlineData("The city and state are missing or together unverifiable.", AddressValidationStatus.InvalidCityState)]
    [InlineData("The address information in the request is insufficient to match.", AddressValidationStatus.InsufficientInput)]
    [InlineData("The address requested is an invalid delivery address.", AddressValidationStatus.InvalidAddress)]
    [InlineData("More than one address was found matching the requested address.", AddressValidationStatus.MultipleMatches)]
    [InlineData("The ZIP Code in the request is missing or invalid.", AddressValidationStatus.InvalidZipCode)]
    public void Maps_the_documented_usps_error_messages(string message, AddressValidationStatus expected)
    {
        Assert.Equal(expected, UspsStatusMapper.FromErrorMessage(message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Something entirely unexpected happened.")]
    public void Falls_back_to_address_not_found_for_unrecognised_messages(string? message)
    {
        Assert.Equal(AddressValidationStatus.AddressNotFound, UspsStatusMapper.FromErrorMessage(message));
    }

    [Fact]
    public void Falls_back_to_match_codes_when_no_return_code_is_present()
    {
        var response = new AddressResponseDto
        {
            Address = new UspsAddress { StreetAddress = "3120 M ST NW" },
            Matches = [new AddressCode("31", "Single Response - exact match")],
        };

        Assert.Equal(AddressValidationStatus.Validated, UspsStatusMapper.FromSuccessResponse(response));
    }

    [Fact]
    public void Falls_back_to_correction_codes_when_no_return_code_is_present()
    {
        var response = new AddressResponseDto
        {
            Address = new UspsAddress { StreetAddress = "1600 PENNSYLVANIA AVE NW" },
            Corrections = [new AddressCode("32", "More information is needed.")],
        };

        Assert.Equal(AddressValidationStatus.DefaultAddress, UspsStatusMapper.FromSuccessResponse(response));
    }

    [Fact]
    public void Treats_a_bare_address_carrying_zip_plus_four_as_a_match()
    {
        var response = new AddressResponseDto
        {
            Address = new UspsAddress { StreetAddress = "3120 M ST NW", ZipCode = "20007", ZipPlus4 = "3704" },
        };

        Assert.Equal(AddressValidationStatus.Validated, UspsStatusMapper.FromSuccessResponse(response));
    }

    [Fact]
    public void Treats_a_bare_address_without_zip_plus_four_as_needing_more_information()
    {
        var response = new AddressResponseDto
        {
            Address = new UspsAddress { StreetAddress = "3120 M ST NW", ZipCode = "20007" },
        };

        Assert.Equal(AddressValidationStatus.DefaultAddress, UspsStatusMapper.FromSuccessResponse(response));
    }

    [Fact]
    public void Reports_unknown_when_no_address_and_no_codes_came_back()
    {
        Assert.Equal(AddressValidationStatus.Unknown, UspsStatusMapper.FromSuccessResponse(new AddressResponseDto()));
    }
}
