namespace Forge3D.Core.Data.Schema;

public sealed class SchemaDetector
{
    public IReadOnlyList<string> DetectFields(ParsedDataSet dataSet)
    {
        return dataSet.Records
            .SelectMany(record => record.Fields.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(field => field)
            .ToList();
    }

    public FieldMappingProfile RecommendMapping(ParsedDataSet dataSet)
    {
        var fields = DetectFields(dataSet);
        var profile = new FieldMappingProfile();
        profile.Map(ForgeField.Timestamp, Find(fields, "timestamp", "time", "ts"));
        profile.Map(ForgeField.EntityId, Find(fields, "entity_id", "entityid", "vehicle_id", "id"));
        profile.Map(ForgeField.PositionX, Find(fields, "x", "pos_x", "position.x", "positionX", "vehicle.pose.x"));
        profile.Map(ForgeField.PositionY, Find(fields, "y", "pos_y", "position.y", "positionY", "vehicle.pose.y"));
        profile.Map(ForgeField.PositionZ, Find(fields, "z", "pos_z", "position.z", "positionZ", "vehicle.pose.z"));
        profile.Map(ForgeField.Speed, Find(fields, "speed", "vel", "velocity"));
        profile.Map(ForgeField.Heading, Find(fields, "heading", "yaw", "heading_deg"));
        return profile;
    }

    private static string? Find(IReadOnlyList<string> fields, params string[] candidates)
    {
        return candidates
            .Select(candidate => fields.FirstOrDefault(field => string.Equals(field, candidate, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(match => !string.IsNullOrWhiteSpace(match));
    }
}
