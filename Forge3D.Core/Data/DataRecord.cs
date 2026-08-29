namespace Forge3D.Core.Data;

public sealed class DataRecord
{
    public double? Timestamp { get; set; }

    public string? EntityId { get; set; }

    public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
}
