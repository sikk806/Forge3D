using System.Text.Json;
using Forge3D.Core.Data;

namespace Forge3D.Core.Data.Parsing;

public sealed class JsonDataParser : IDataParser
{
    public string Format => "JSON";

    public bool CanParse(string fileName, string content)
    {
        return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || content.TrimStart().StartsWith('{') || content.TrimStart().StartsWith('[');
    }

    public ParsedDataSet Parse(string content)
    {
        using var document = JsonDocument.Parse(content);
        var records = new List<DataRecord>();

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                records.Add(ToRecord(item));
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object
                 && document.RootElement.TryGetProperty("records", out var recordsElement)
                 && recordsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in recordsElement.EnumerateArray())
            {
                records.Add(ToRecord(item));
            }
        }
        else
        {
            records.Add(ToRecord(document.RootElement));
        }

        return new ParsedDataSet(Format, records);
    }

    private static DataRecord ToRecord(JsonElement element)
    {
        var record = new DataRecord();
        Flatten(element, string.Empty, record.Fields);
        return record;
    }

    private static void Flatten(JsonElement element, string prefix, IDictionary<string, string> fields)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}.{property.Name}";
                Flatten(property.Value, key, fields);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                Flatten(item, $"{prefix}[{index++}]", fields);
            }

            return;
        }

        fields[prefix] = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }
}
