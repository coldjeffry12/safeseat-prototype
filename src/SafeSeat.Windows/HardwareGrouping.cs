using System.Security.Cryptography;
using System.Text;

namespace SafeSeat.Windows;

/// <summary>
/// Adds optional PnP correlation metadata without changing a raw collection's identity, assignment
/// eligibility, or routing. A candidate group is not a count or confirmation of physical devices.
/// </summary>
public static class HardwareGrouping
{
    public static RawDeviceDescriptor WithMetadata(RawDeviceDescriptor collection, Guid? containerId, string? enumeratorName)
    {
        ArgumentNullException.ThrowIfNull(collection);
        string? group = CandidateGroupId(containerId);
        return collection with
        {
            CandidateGroupId = group,
            TransportHint = DescribeEnumerator(enumeratorName),
            CorrelationNote = group is null
                ? "PnP container metadata unavailable; no group is inferred from labels or enumerator names. Physical source remains unverified."
                : "Candidate PnP container correlation only, not a confirmed physical device or physical-device count. A receiver/container may cover multiple physical devices; virtual or remote sources may appear. Confirm each collection using hardware tests."
        };
    }

    /// <summary>Hashes a canonical GUID in memory; raw container IDs never enter the returned DTO.</summary>
    public static string? CandidateGroupId(Guid? containerId)
    {
        if (containerId is null || containerId.Value == Guid.Empty)
            return null;
        string canonical = containerId.Value.ToString("D").ToUpperInvariant();
        return "pnp-container-sha256-" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    /// <summary>
    /// Enumerator names describe devnode provenance, not validated transport or physicality.
    /// Return only allowlisted descriptions so arbitrary property text cannot expose paths or serials.
    /// </summary>
    public static string? DescribeEnumerator(string? enumeratorName) => enumeratorName?.Trim().ToUpperInvariant() switch
    {
        "HID" => "HID enumerator; underlying transport and physical source unknown",
        "USB" => "USB enumerator hint; physical hardware unverified",
        "BTH" or "BTHENUM" or "BTHLE" or "BTHLEDEVICE" => "Bluetooth enumerator hint; physical source unverified",
        "ROOT" => "ROOT enumerator; transport and physical source unknown",
        "SWD" or "SWENUM" => "Software enumerator hint; underlying source unverified",
        "RDPBUS" or "TERMSRV" => "Remote/session enumerator hint; underlying source unverified",
        "ACPI" => "ACPI enumerator hint; source relationship unverified",
        _ => null
    };
}
