namespace USPS.AddressValidation.Models;

/// <summary>
/// How USPS altered one field of the submitted address.
/// </summary>
public enum AddressChangeKind
{
    /// <summary>The field was empty on input and USPS supplied a value — a completed ZIP+4, for example.</summary>
    Completed = 0,

    /// <summary>The field had a value on input and USPS standardized it to a different one.</summary>
    Corrected,
}

/// <summary>
/// A single difference between the submitted address and the standardized address USPS returned.
/// </summary>
/// <param name="Field">Name of the changed field, matching the property on <see cref="UspsAddress"/>.</param>
/// <param name="Original">The submitted value, or <see langword="null"/> when nothing was submitted.</param>
/// <param name="Standardized">The value USPS returned.</param>
/// <param name="Kind">Whether the value was completed or corrected.</param>
public sealed record AddressFieldChange(
    string Field,
    string? Original,
    string? Standardized,
    AddressChangeKind Kind)
{
    /// <inheritdoc />
    public override string ToString() => Kind == AddressChangeKind.Completed
        ? $"{Field}: (empty) -> '{Standardized}'"
        : $"{Field}: '{Original}' -> '{Standardized}'";
}
