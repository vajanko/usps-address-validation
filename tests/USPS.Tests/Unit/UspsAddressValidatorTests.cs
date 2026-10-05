using USPS.AddressValidation;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using USPS.AddressValidation.Exceptions;
using USPS.AddressValidation.Models;
using USPS.Tests.Infrastructure;

namespace USPS.Tests.Unit;

/// <summary>
/// The client's behaviour against every response shape the USPS Addresses API can produce.
/// </summary>
public sealed class UspsAddressValidatorTests
{
    private static readonly AddressInput ValidInput =
        AddressInput.Create("3120 M St NW", city: "Washington", state: "DC");

    // ---------------------------------------------------------------- successful validation

    [Fact]
    public async Task Exact_match_is_reported_as_validated_with_the_standardized_address()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.OK, TestPayloads.ExactMatch));

        var result = await harness.ValidateAsync(ValidInput);

        Assert.Equal(AddressValidationStatus.Validated, result.Status);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);

        var address = Assert.IsType<UspsAddress>(result.Address);
        Assert.Equal("3120 M ST NW", address.StreetAddress);
        Assert.Equal("WASHINGTON", address.City);
        Assert.Equal("DC", address.State);
        Assert.Equal("20007", address.ZipCode);
        Assert.Equal("3704", address.ZipPlus4);
        Assert.Equal("20007-3704", address.FullZipCode);
    }

    [Fact]
    public async Task Completed_and_corrected_fields_are_reported_as_changes()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.OK, TestPayloads.ExactMatch));

        var result = await harness.ValidateAsync(
            AddressInput.Create("3120 m st nw", city: "Washington", state: "DC"));

        Assert.True(result.WasModified);

        // The ZIP Code and ZIP+4 were absent on input and supplied by USPS.
        var zip = Assert.Single(result.Changes, c => c.Field == nameof(UspsAddress.ZipCode));
        Assert.Equal(AddressChangeKind.Completed, zip.Kind);
        Assert.Equal("20007", zip.Standardized);

        var zipPlus4 = Assert.Single(result.Changes, c => c.Field == nameof(UspsAddress.ZipPlus4));
        Assert.Equal(AddressChangeKind.Completed, zipPlus4.Kind);
        Assert.Equal("3704", zipPlus4.Standardized);

        // The city and state came back unchanged apart from casing, which is not a correction.
        Assert.DoesNotContain(result.Changes, c => c.Field == nameof(UspsAddress.City));
        Assert.DoesNotContain(result.Changes, c => c.Field == nameof(UspsAddress.State));
        Assert.DoesNotContain(result.Changes, c => c.Field == nameof(UspsAddress.StreetAddress));
    }

    [Fact]
    public async Task Street_address_standardization_is_reported_as_a_correction()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.OK, TestPayloads.ExactMatch));

        var result = await harness.ValidateAsync(
            AddressInput.Create("3120 M Street Northwest", city: "Washington", state: "DC"));

        var change = Assert.Single(result.Changes, c => c.Field == nameof(UspsAddress.StreetAddress));
        Assert.Equal(AddressChangeKind.Corrected, change.Kind);
        Assert.Equal("3120 M Street Northwest", change.Original);
        Assert.Equal("3120 M ST NW", change.Standardized);
    }

    [Fact]
    public async Task Delivery_point_metadata_is_surfaced()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.OK, TestPayloads.ExactMatch));

        var result = await harness.ValidateAsync(ValidInput);

        var info = Assert.IsType<AddressAdditionalInfo>(result.AdditionalInfo);
        Assert.Equal(31, info.ReturnCode);
        Assert.Equal("Y", info.DpvConfirmation);
        Assert.True(info.IsDeliveryPointConfirmed);
        Assert.True(info.IsBusiness);
        Assert.False(info.IsVacant);
        Assert.Equal("C036", info.CarrierRoute);
        Assert.Equal("DISTRICT OF COLUMBIA", info.CountyName);

        // Fields not modelled explicitly are still available.
        Assert.True(info.Extensions.ContainsKey("electricVehicleRoute"));

        var match = Assert.Single(result.Matches);
        Assert.Equal("31", match.Code);
    }

    // ---------------------------------------------------------------- partial / ambiguous answers

    [Fact]
    public async Task Default_match_reports_that_more_information_is_needed()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.OK, TestPayloads.DefaultMatch));

        var result = await harness.ValidateAsync(
            AddressInput.Create("1600 Pennsylvania Ave NW", city: "Washington", state: "DC"));

        Assert.Equal(AddressValidationStatus.DefaultAddress, result.Status);
        Assert.False(result.IsValid);
        Assert.True(result.HasAddress);
        Assert.Equal("20500", result.Address!.ZipCode);

        var correction = Assert.Single(result.Corrections);
        Assert.Equal("32", correction.Code);
        Assert.Single(result.Warnings);
        Assert.True(result.AdditionalInfo!.IsSecondaryUnitProblem);
    }

    [Fact]
    public async Task Multiple_matches_returned_with_http_200_are_classified()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.OK, TestPayloads.MultipleMatch));

        var result = await harness.ValidateAsync(
            AddressInput.Create("100 Main St", city: "Springfield", state: "IL"));

        Assert.Equal(AddressValidationStatus.MultipleMatches, result.Status);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(ValidationErrorSource.Usps, result.Errors[0].Source);
    }

    [Fact]
    public async Task Multiple_matches_returned_as_a_404_envelope_are_classified()
    {
        using var harness = TestHarness.WithToken(
            h => h.EnqueueJson(HttpStatusCode.NotFound, TestPayloads.MultipleAddressesError));

        var result = await harness.ValidateAsync(ValidInput);

        Assert.Equal(AddressValidationStatus.MultipleMatches, result.Status);
        Assert.False(result.HasAddress);
    }

    // ---------------------------------------------------------------- addresses USPS rejects

    [Fact]
    public async Task Unknown_address_is_reported_as_a_result_not_an_exception()
    {
        using var harness = TestHarness.WithToken(
            h => h.EnqueueJson(HttpStatusCode.NotFound, TestPayloads.AddressNotFoundError));

        var result = await harness.ValidateAsync(
            AddressInput.Create("9999 Nowhere Rd", city: "Washington", state: "DC"));

        Assert.Equal(AddressValidationStatus.AddressNotFound, result.Status);
        Assert.False(result.IsValid);
        Assert.Null(result.Address);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorSource.Usps, error.Source);
        Assert.Equal("404", error.Code);
        Assert.Equal("There is no match for the address requested.", error.Message);
        Assert.Equal(error.Message, result.ErrorMessage);
    }

    [Fact]
    public async Task Invalid_state_reported_by_usps_is_classified()
    {
        using var harness = TestHarness.WithToken(
            h => h.EnqueueJson(HttpStatusCode.NotFound, TestPayloads.InvalidStateError));

        // A state code this library accepts but USPS does not recognise for the address.
        var result = await harness.ValidateAsync(
            AddressInput.Create("3120 M St NW", city: "Washington", state: "AK"));

        Assert.Equal(AddressValidationStatus.InvalidState, result.Status);
    }

    [Fact]
    public async Task Bad_request_details_are_flattened_into_errors()
    {
        using var harness = TestHarness.WithToken(
            h => h.EnqueueJson(HttpStatusCode.BadRequest, TestPayloads.BadRequestError));

        var result = await harness.ValidateAsync(ValidInput);

        Assert.Equal(AddressValidationStatus.InsufficientInput, result.Status);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Field == "ZIPCode" && e.Code == "1000");
    }

    [Fact]
    public async Task Non_json_error_body_still_produces_a_result()
    {
        using var harness = TestHarness.WithToken(h => h.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("<html>Not Found</html>", Encoding.UTF8, "text/html"),
        }));

        var result = await harness.ValidateAsync(ValidInput);

        Assert.Equal(AddressValidationStatus.AddressNotFound, result.Status);
        Assert.NotEmpty(result.Errors);
    }

    // ---------------------------------------------------------------- local input validation

    [Fact]
    public async Task Locally_invalid_input_short_circuits_before_any_http_call()
    {
        using var harness = TestHarness.Raw(_ => { });

        var result = await harness.ValidateAsync(AddressInput.Create("3120 M St NW", state: "ZZ"));

        Assert.Equal(AddressValidationStatus.InvalidInput, result.Status);
        Assert.False(result.IsValid);
        Assert.All(result.Errors, e => Assert.Equal(ValidationErrorSource.Local, e.Source));
        Assert.Equal(0, harness.Handler.RequestCount);
    }

    [Fact]
    public async Task Null_input_throws_argument_null_exception()
    {
        using var harness = TestHarness.Raw(_ => { });

        await Assert.ThrowsAsync<ArgumentNullException>(() => harness.ValidateAsync(null!));
    }

    // ---------------------------------------------------------------- the request that goes out

    [Fact]
    public async Task Request_carries_the_bearer_token_and_every_supplied_field()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.OK, TestPayloads.ExactMatch));

        await harness.ValidateAsync(new AddressInput
        {
            Firm = "Acme & Co",
            StreetAddress = "3120 M St NW",
            SecondaryAddress = "Ste 200",
            City = "Washington",
            State = "dc",
            ZipCode = "20007",
            ZipPlus4 = "3704",
        });

        var request = Assert.Single(harness.Handler.AddressRequests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/addresses/v3/address", request.RequestUri!.AbsolutePath);

        var auth = Assert.IsType<AuthenticationHeaderValue>(request.Headers.Authorization);
        Assert.Equal("Bearer", auth.Scheme);
        Assert.Equal("test-access-token", auth.Parameter);

        var query = request.RequestUri.Query;
        Assert.Contains("streetAddress=3120%20M%20St%20NW", query, StringComparison.Ordinal);
        Assert.Contains("secondaryAddress=Ste%20200", query, StringComparison.Ordinal);
        Assert.Contains("city=Washington", query, StringComparison.Ordinal);
        Assert.Contains("ZIPCode=20007", query, StringComparison.Ordinal);
        Assert.Contains("ZIPPlus4=3704", query, StringComparison.Ordinal);
        Assert.Contains("firm=Acme%20%26%20Co", query, StringComparison.Ordinal);

        // The state is upper-cased because USPS matches it against a case-sensitive pattern.
        Assert.Contains("state=DC", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Optional_fields_are_omitted_from_the_query()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.OK, TestPayloads.ExactMatch));

        await harness.ValidateAsync(ValidInput);

        var query = Assert.Single(harness.Handler.AddressRequests).RequestUri!.Query;
        Assert.DoesNotContain("firm=", query, StringComparison.Ordinal);
        Assert.DoesNotContain("ZIPCode=", query, StringComparison.Ordinal);
        Assert.DoesNotContain("urbanization=", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Configured_user_id_header_is_sent()
    {
        var options = TestHarness.DefaultOptions();
        options.UserId = "user-42";

        using var harness = TestHarness.WithToken(
            h => h.EnqueueJson(HttpStatusCode.OK, TestPayloads.ExactMatch), options);

        await harness.ValidateAsync(ValidInput);

        var request = Assert.Single(harness.Handler.AddressRequests);
        Assert.Equal("user-42", Assert.Single(request.Headers.GetValues("X-User-Id")));
    }

    // ---------------------------------------------------------------- failures of the call itself

    [Fact]
    public void Missing_client_id_throws_a_configuration_exception()
    {
        var options = TestHarness.DefaultOptions();
        options.ClientId = null;

        var exception = Assert.Throws<UspsConfigurationException>(() => TestHarness.Raw(_ => { }, options));
        Assert.Contains(nameof(UspsOptions.ClientId), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_client_secret_throws_a_configuration_exception()
    {
        var options = TestHarness.DefaultOptions();
        options.ClientSecret = "  ";

        Assert.Throws<UspsConfigurationException>(() => TestHarness.Raw(_ => { }, options));
    }

    [Fact]
    public async Task Unreachable_service_throws_a_transport_exception()
    {
        using var harness = TestHarness.Raw(h => h
            .EnqueueToken()
            .EnqueueThrow(new HttpRequestException("No such host is known.")));

        var exception = await Assert.ThrowsAsync<UspsTransportException>(
            () => harness.ValidateAsync(ValidInput));

        Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Contains("Could not reach", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unreachable_token_endpoint_throws_a_transport_exception()
    {
        using var harness = TestHarness.Raw(h => h.EnqueueThrow(new HttpRequestException("Connection refused.")));

        var exception = await Assert.ThrowsAsync<UspsTransportException>(
            () => harness.ValidateAsync(ValidInput));

        Assert.Contains("token endpoint", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Timeout_throws_a_transport_exception()
    {
        using var harness = TestHarness.Raw(h => h
            .EnqueueToken()
            .EnqueueThrow(new TaskCanceledException("The request timed out.")));

        var exception = await Assert.ThrowsAsync<UspsTransportException>(
            () => harness.ValidateAsync(ValidInput));

        Assert.Contains("timed out", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejected_credentials_at_the_token_endpoint_throw_an_authentication_exception()
    {
        using var harness = TestHarness.Raw(
            h => h.EnqueueJson(HttpStatusCode.Unauthorized, TestPayloads.OAuthInvalidClientError));

        var exception = await Assert.ThrowsAsync<UspsAuthenticationException>(
            () => harness.ValidateAsync(ValidInput));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal("invalid_client", exception.ErrorCode);
        Assert.Contains("Client credentials are invalid.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expired_token_is_refreshed_once_and_the_request_retried()
    {
        using var harness = TestHarness.Raw(h => h
            .EnqueueToken("stale-token")
            .EnqueueJson(HttpStatusCode.Unauthorized, TestPayloads.UnauthorizedError)
            .EnqueueToken("fresh-token")
            .EnqueueJson(HttpStatusCode.OK, TestPayloads.ExactMatch));

        var result = await harness.ValidateAsync(ValidInput);

        Assert.Equal(AddressValidationStatus.Validated, result.Status);
        Assert.Equal(2, harness.Handler.TokenRequests.Count);
        Assert.Equal(2, harness.Handler.AddressRequests.Count);
        Assert.Equal("fresh-token", harness.Handler.AddressRequests[1].Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task Repeated_unauthorized_responses_throw_an_authentication_exception()
    {
        using var harness = TestHarness.Raw(h => h
            .EnqueueToken()
            .EnqueueJson(HttpStatusCode.Unauthorized, TestPayloads.UnauthorizedError)
            .EnqueueToken()
            .EnqueueJson(HttpStatusCode.Unauthorized, TestPayloads.UnauthorizedError));

        var exception = await Assert.ThrowsAsync<UspsAuthenticationException>(
            () => harness.ValidateAsync(ValidInput));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal("401", exception.ErrorCode);
    }

    [Fact]
    public async Task Forbidden_explains_the_missing_api_license()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.Forbidden, "{}"));

        var exception = await Assert.ThrowsAsync<UspsAuthenticationException>(
            () => harness.ValidateAsync(ValidInput));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Contains("Addresses API license", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Throttling_throws_a_rate_limit_exception_carrying_retry_after()
    {
        using var harness = TestHarness.WithToken(h => h.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        }));

        var exception = await Assert.ThrowsAsync<UspsRateLimitException>(
            () => harness.ValidateAsync(ValidInput));

        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
    }

    [Fact]
    public async Task Server_error_throws_an_api_exception()
    {
        using var harness = TestHarness.WithToken(
            h => h.EnqueueJson(HttpStatusCode.InternalServerError, TestPayloads.ServerError));

        var exception = await Assert.ThrowsAsync<UspsApiException>(
            () => harness.ValidateAsync(ValidInput));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal("500", exception.ErrorCode);
        Assert.Contains("An unexpected error occurred.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unparseable_success_body_throws_a_serialization_exception()
    {
        using var harness = TestHarness.WithToken(h => h.EnqueueJson(HttpStatusCode.OK, "{ not json"));

        var exception = await Assert.ThrowsAsync<UspsSerializationException>(
            () => harness.ValidateAsync(ValidInput));

        Assert.Equal("{ not json", exception.ResponseBody);
    }

    [Fact]
    public async Task Cancellation_is_propagated_rather_than_wrapped()
    {
        using var cts = new CancellationTokenSource();
        using var harness = TestHarness.Raw(h => h.EnqueueThrow(new TaskCanceledException()));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Validator.ValidateAsync(ValidInput, cts.Token));
    }

    [Fact]
    public async Task Every_failure_derives_from_the_common_base_exception()
    {
        using var harness = TestHarness.WithToken(
            h => h.EnqueueJson(HttpStatusCode.InternalServerError, TestPayloads.ServerError));

        await Assert.ThrowsAnyAsync<UspsException>(() => harness.ValidateAsync(ValidInput));
    }
}
