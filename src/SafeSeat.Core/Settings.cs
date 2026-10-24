using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafeSeat.Core;

/// <summary>Only redacted assignments and a schema version are persisted. Never contains input text.</summary>
public sealed class Settings
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public Dictionary<string, SeatId> DeviceAssignments { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<SeatId, string> MonitorAssignments { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public IReadOnlyList<string> Validate(IEnumerable<DeviceDescriptor>? devices = null)
    {
        var errors = new List<string>();
        if (Version != CurrentVersion) errors.Add($"Unsupported settings version {Version}.");
        if (DeviceAssignments is null) errors.Add("Device assignments must be an object.");
        if (MonitorAssignments is null) errors.Add("Monitor assignments must be an object.");
        if (DeviceAssignments is not null)
        {
            foreach (var (id, seat) in DeviceAssignments)
            {
                if (!ValidId(id)) errors.Add("A device identity is empty, too long, or contains control characters.");
                if (!Enum.IsDefined(seat)) errors.Add("A device assignment has an invalid seat.");
            }
            if (devices is not null)
            {
                var known = devices.GroupBy(d => d.Id, StringComparer.Ordinal)
                    .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
                foreach (var seat in Enum.GetValues<SeatId>())
                    foreach (var kind in Enum.GetValues<DeviceKind>())
                        if (DeviceAssignments.Count(a => a.Value == seat && known.TryGetValue(a.Key, out var d) && d.Kind == kind) > 1)
                            errors.Add($"Seat {seat} has more than one {kind.ToString().ToLowerInvariant()} assigned.");
            }
        }
        if (MonitorAssignments is not null)
        {
            foreach (var (seat, id) in MonitorAssignments)
            {
                if (!Enum.IsDefined(seat)) errors.Add("A monitor assignment has an invalid seat.");
                if (!ValidId(id)) errors.Add("A monitor identity is invalid.");
            }
            if (MonitorAssignments.Values.Distinct(StringComparer.Ordinal).Count() != MonitorAssignments.Count)
                errors.Add("The two seats must use different monitors.");
        }
        return errors;
    }

    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 256 && !id.Any(char.IsControl);

    public string ToJson()
    {
        var errors = Validate();
        if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public static Settings Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 1_048_576) throw new InvalidDataException("Settings exceed the size limit.");
        RejectDuplicateProperties(json);
        var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions)
            ?? throw new InvalidDataException("Settings must be an object.");
        var errors = settings.Validate();
        if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
        return settings;
    }

    // System.Text.Json accepts duplicate object names by default; ambiguous assignments must fail closed.
    private static void RejectDuplicateProperties(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        Visit(document.RootElement);
        // Enum dictionary keys are case-insensitive even though ordinary JSON properties and
        // device identities are ordinal. Reject aliases before deserialization can overwrite a seat.
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty(nameof(MonitorAssignments), out var monitors) &&
            monitors.ValueKind == JsonValueKind.Object)
        {
            var seats = new HashSet<SeatId>();
            foreach (var property in monitors.EnumerateObject())
                if (Enum.TryParse<SeatId>(property.Name, ignoreCase: true, out var seat) &&
                    Enum.IsDefined(seat) && !seats.Add(seat))
                    throw new InvalidDataException("Duplicate monitor seat assignments are not allowed.");
        }
        static void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate settings properties are not allowed.");
                    Visit(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var child in element.EnumerateArray()) Visit(child);
        }
    }
}

/// <summary>Same-directory temporary write and atomic replacement; no elevation or registry changes.</summary>
public static class SettingsStore
{
    public static Settings Load(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return new Settings();
        if (info.Length > 1_048_576) throw new InvalidDataException("Settings exceed the size limit.");
        return Settings.Parse(File.ReadAllText(path));
    }

    public static void SaveAtomic(string path, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var json = settings.ToJson();
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 1024, leaveOpen: true);
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
