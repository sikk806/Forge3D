using System.Globalization;

namespace Forge3D.Core.Data.Environment;

public sealed class EnvironmentBuilder
{
    public IReadOnlyList<EnvironmentObstacleSpec> BuildObstacles(ParsedDataSet dataSet)
    {
        var obstacles = new List<EnvironmentObstacleSpec>();

        foreach (var record in dataSet.Records)
        {
            if (!TryRead(record, ["x", "pos_x", "position.x"], out var x)
                || !TryRead(record, ["z", "pos_z", "position.z"], out var z))
            {
                continue;
            }

            var id = TryReadString(record, ["id", "obstacle_id", "name"]) ?? $"obstacle-{obstacles.Count + 1}";
            var width = TryRead(record, ["width", "size_x"], out var parsedWidth) ? parsedWidth : 1.0f;
            var depth = TryRead(record, ["depth", "size_z", "length"], out var parsedDepth) ? parsedDepth : 1.0f;
            obstacles.Add(new EnvironmentObstacleSpec(id, x, z, Math.Max(0.1f, width), Math.Max(0.1f, depth)));
        }

        return obstacles;
    }

    private static bool TryRead(DataRecord record, string[] candidates, out float value)
    {
        value = 0.0f;
        var text = TryReadString(record, candidates);
        return text is not null && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string? TryReadString(DataRecord record, string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (record.Fields.TryGetValue(candidate, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
