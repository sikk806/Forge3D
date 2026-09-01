using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge3D.Contracts.Connection;

public static class SimulationNetworkOptions
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        return new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}
