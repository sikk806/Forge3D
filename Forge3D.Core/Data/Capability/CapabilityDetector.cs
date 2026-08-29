using Forge3D.Core.Data.Schema;

namespace Forge3D.Core.Data.Capability;

public sealed class CapabilityDetector
{
    public IReadOnlySet<ImportCapability> Detect(FieldMappingProfile mapping)
    {
        var capabilities = new HashSet<ImportCapability>();

        if (mapping.TryGetSourceField(ForgeField.Timestamp, out _))
        {
            capabilities.Add(ImportCapability.TimeSeries);
        }

        var hasPosition = mapping.TryGetSourceField(ForgeField.PositionX, out _)
            && mapping.TryGetSourceField(ForgeField.PositionY, out _)
            && mapping.TryGetSourceField(ForgeField.PositionZ, out _);
        if (hasPosition)
        {
            capabilities.Add(ImportCapability.Position);
            capabilities.Add(ImportCapability.Replay3D);
        }

        if (mapping.TryGetSourceField(ForgeField.Speed, out _))
        {
            capabilities.Add(ImportCapability.Velocity);
            capabilities.Add(ImportCapability.Graph);
        }

        if (mapping.TryGetSourceField(ForgeField.Heading, out _))
        {
            capabilities.Add(ImportCapability.Heading);
        }

        if (mapping.TryGetSourceField(ForgeField.Temperature, out _))
        {
            capabilities.Add(ImportCapability.Temperature);
            capabilities.Add(ImportCapability.Graph);
        }

        if (mapping.TryGetSourceField(ForgeField.Status, out _))
        {
            capabilities.Add(ImportCapability.EventAnalysis);
        }

        return capabilities;
    }
}
