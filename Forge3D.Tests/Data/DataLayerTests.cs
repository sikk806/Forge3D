using Forge3D.Core.Data.Capability;
using Forge3D.Core.Data.Environment;
using Forge3D.Core.Data.Parsing;
using Forge3D.Core.Data.Schema;
using Forge3D.Core.Data.Time;
using Forge3D.Core.Data.Validation;

namespace Forge3D.Tests.Data;

public sealed class DataLayerTests
{
    [Fact]
    public void CsvParser_ParsesHeaderFields()
    {
        var parser = new CsvDataParser();

        var dataSet = parser.Parse("time,vehicle_id,pos_x,pos_y,pos_z,speed\n0.1,V1,1,0,2,3.5");

        Assert.Single(dataSet.Records);
        Assert.Equal("1", dataSet.Records[0].Fields["pos_x"]);
        Assert.Equal("3.5", dataSet.Records[0].Fields["speed"]);
    }

    [Fact]
    public void JsonParser_FlattensNestedObjects()
    {
        var parser = new JsonDataParser();

        var dataSet = parser.Parse("""[{"vehicle":{"pose":{"x":1.2,"z":3.4}},"speed":2.5}]""");

        Assert.Single(dataSet.Records);
        Assert.Equal("1.2", dataSet.Records[0].Fields["vehicle.pose.x"]);
        Assert.Equal("3.4", dataSet.Records[0].Fields["vehicle.pose.z"]);
    }

    [Fact]
    public void SchemaDetector_RecommendsCommonFieldMappings()
    {
        var dataSet = new CsvDataParser().Parse("time,vehicle_id,pos_x,pos_y,pos_z,velocity\n0,V1,1,0,2,3");

        var mapping = new SchemaDetector().RecommendMapping(dataSet);

        Assert.True(mapping.TryGetSourceField(ForgeField.Timestamp, out var timestamp));
        Assert.Equal("time", timestamp);
        Assert.True(mapping.TryGetSourceField(ForgeField.Speed, out var speed));
        Assert.Equal("velocity", speed);
    }

    [Fact]
    public void DataValidator_ReportsInvalidRows()
    {
        var dataSet = new CsvDataParser().Parse("time,pos_x,pos_y,pos_z\n0,1,0,2\n1,,0,3");
        var mapping = new SchemaDetector().RecommendMapping(dataSet);

        var result = new DataValidator().Validate(dataSet, mapping, [ForgeField.PositionX, ForgeField.PositionY, ForgeField.PositionZ]);

        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(1, result.ValidRecords);
        Assert.Equal(1, result.InvalidRecords);
    }

    [Fact]
    public void CapabilityDetector_DetectsReplayAndGraph()
    {
        var mapping = new FieldMappingProfile();
        mapping.Map(ForgeField.PositionX, "x");
        mapping.Map(ForgeField.PositionY, "y");
        mapping.Map(ForgeField.PositionZ, "z");
        mapping.Map(ForgeField.Speed, "speed");

        var capabilities = new CapabilityDetector().Detect(mapping);

        Assert.Contains(ImportCapability.Replay3D, capabilities);
        Assert.Contains(ImportCapability.Graph, capabilities);
    }

    [Fact]
    public void TimestampConverter_HandlesUnixMilliseconds()
    {
        var converted = new TimestampConverter().TryConvert("1700000000000", 0, 0.0, out var seconds);

        Assert.True(converted);
        Assert.Equal(1700000000.0, seconds);
    }

    [Fact]
    public void EnvironmentBuilder_CreatesObstacleSpecsFromData()
    {
        var dataSet = new CsvDataParser().Parse("id,x,z,width,depth\nobs-1,1.5,2.5,0.8,1.2");

        var obstacles = new EnvironmentBuilder().BuildObstacles(dataSet);

        Assert.Single(obstacles);
        Assert.Equal("obs-1", obstacles[0].Id);
        Assert.Equal(1.5f, obstacles[0].X);
        Assert.Equal(1.2f, obstacles[0].Depth);
    }
}
