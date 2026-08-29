using Forge3D.Core.Data;

namespace Forge3D.Core.Data.Parsing;

public sealed class CsvDataParser : IDataParser
{
    public string Format => "CSV";

    public bool CanParse(string fileName, string content)
    {
        return fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || FirstNonEmptyLine(content).Contains(',');
    }

    public ParsedDataSet Parse(string content)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            return new ParsedDataSet(Format, []);
        }

        var headers = SplitLine(lines[0]);
        var records = new List<DataRecord>();

        for (var i = 1; i < lines.Length; i++)
        {
            var values = SplitLine(lines[i]);
            var record = new DataRecord();
            for (var column = 0; column < headers.Count && column < values.Count; column++)
            {
                record.Fields[headers[column]] = values[column];
            }

            records.Add(record);
        }

        return new ParsedDataSet(Format, records);
    }

    private static string FirstNonEmptyLine(string content)
    {
        return content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
    }

    private static IReadOnlyList<string> SplitLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }
}
