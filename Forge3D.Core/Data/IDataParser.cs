namespace Forge3D.Core.Data;

public interface IDataParser
{
    string Format { get; }

    bool CanParse(string fileName, string content);

    ParsedDataSet Parse(string content);
}
