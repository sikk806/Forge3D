namespace Forge3D.Core.Data;

public sealed class ParsedDataSet
{
    public ParsedDataSet(string format, IReadOnlyList<DataRecord> records)
    {
        Format = format;
        Records = records;
    }

    public string Format { get; }

    public IReadOnlyList<DataRecord> Records { get; }
}
