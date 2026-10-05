using USPS.AddressValidation.Models;
using USPS.AddressValidation.Validation;

namespace USPS.Tests.Unit;

/// <summary>
/// Local input checks, which run before any call is made to USPS.
/// </summary>
public sealed class AddressInputValidatorTests
{
    [Fact]
    public void Accepts_street_with_city_and_state()
    {
        var errors = AddressInputValidator.Validate(
            AddressInput.Create("3120 M St NW", city: "Washington", state: "DC"));

        Assert.Empty(errors);
    }

    [Fact]
    public void Accepts_street_with_zip_only()
    {
        var errors = AddressInputValidator.Validate(
            AddressInput.Create("3120 M St NW", zipCode: "20007"));

        Assert.Empty(errors);
    }

    [Fact]
    public void Accepts_lowercase_state_code()
    {
        var errors = AddressInputValidator.Validate(
            AddressInput.Create("3120 M St NW", city: "Washington", state: "dc"));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_missing_street_address(string? streetAddress)
    {
        var errors = AddressInputValidator.Validate(new AddressInput
        {
            StreetAddress = streetAddress,
            City = "Washington",
            State = "DC",
        });

        var error = Assert.Single(errors);
        Assert.Equal(nameof(AddressInput.StreetAddress), error.Field);
        Assert.Equal(ValidationErrorSource.Local, error.Source);
    }

    [Fact]
    public void Rejects_street_address_over_fifty_characters()
    {
        var errors = AddressInputValidator.Validate(
            AddressInput.Create(new string('A', 51), city: "Washington", state: "DC"));

        Assert.Contains(errors, e => e.Field == nameof(AddressInput.StreetAddress));
    }

    [Fact]
    public void Rejects_missing_city_state_and_zip()
    {
        var errors = AddressInputValidator.Validate(AddressInput.Create("3120 M St NW"));

        var error = Assert.Single(errors);
        Assert.Contains("city and state", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_city_without_state_when_no_zip_is_given()
    {
        var errors = AddressInputValidator.Validate(AddressInput.Create("3120 M St NW", city: "Washington"));

        Assert.Single(errors);
    }

    [Fact]
    public void Rejects_state_without_city_when_no_zip_is_given()
    {
        var errors = AddressInputValidator.Validate(AddressInput.Create("3120 M St NW", state: "DC"));

        Assert.Single(errors);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("D")]
    [InlineData("District of Columbia")]
    [InlineData("D.C")]
    public void Rejects_invalid_state_code(string state)
    {
        var errors = AddressInputValidator.Validate(
            AddressInput.Create("3120 M St NW", city: "Washington", state: state));

        Assert.Contains(errors, e => e.Field == nameof(AddressInput.State));
    }

    [Theory]
    [InlineData("2000")]
    [InlineData("200077")]
    [InlineData("2000A")]
    [InlineData("20007-3704")]
    public void Rejects_malformed_zip_code(string zip)
    {
        var errors = AddressInputValidator.Validate(AddressInput.Create("3120 M St NW", zipCode: zip));

        Assert.Contains(errors, e => e.Field == nameof(AddressInput.ZipCode));
    }

    [Theory]
    [InlineData("370")]
    [InlineData("37044")]
    [InlineData("37O4")]
    public void Rejects_malformed_zip_plus_four(string zipPlus4)
    {
        var errors = AddressInputValidator.Validate(new AddressInput
        {
            StreetAddress = "3120 M St NW",
            ZipCode = "20007",
            ZipPlus4 = zipPlus4,
        });

        Assert.Contains(errors, e => e.Field == nameof(AddressInput.ZipPlus4));
    }

    [Fact]
    public void Rejects_oversized_firm_city_secondary_and_urbanization()
    {
        var errors = AddressInputValidator.Validate(new AddressInput
        {
            Firm = new string('F', 51),
            StreetAddress = "3120 M St NW",
            SecondaryAddress = new string('S', 51),
            City = new string('C', 29),
            State = "DC",
            Urbanization = new string('U', 97),
        });

        Assert.Contains(errors, e => e.Field == nameof(AddressInput.Firm));
        Assert.Contains(errors, e => e.Field == nameof(AddressInput.SecondaryAddress));
        Assert.Contains(errors, e => e.Field == nameof(AddressInput.City));
        Assert.Contains(errors, e => e.Field == nameof(AddressInput.Urbanization));
    }

    [Fact]
    public void Reports_every_problem_at_once()
    {
        var errors = AddressInputValidator.Validate(new AddressInput
        {
            StreetAddress = null,
            City = "Washington",
            State = "XX",
            ZipCode = "ABCDE",
        });

        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void Throws_when_input_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => AddressInputValidator.Validate(null!));
    }

    [Theory]
    [InlineData("DC")]
    [InlineData("PR")]
    [InlineData("AE")]
    [InlineData("GU")]
    [InlineData("MP")]
    public void UspsStates_accepts_territories_and_military_codes(string code)
    {
        Assert.True(UspsStates.IsValid(code));
    }

    [Fact]
    public void UspsStates_matches_the_codes_the_usps_specification_accepts()
    {
        // 50 states + DC + 3 military codes + 8 territories and freely associated states,
        // exactly the set in the Addresses 3.0 state pattern.
        Assert.Equal(62, UspsStates.All.Count);
        Assert.False(UspsStates.IsValid("XX"));
        Assert.False(UspsStates.IsValid(null));
    }
}
