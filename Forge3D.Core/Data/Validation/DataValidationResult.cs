namespace Forge3D.Core.Data.Validation;

public sealed class DataValidationResult
{
    public DataValidationResult(int totalRecords, int validRecords, IReadOnlyList<string> missingFields, IReadOnlyList<int> invalidRows)
    {
        TotalRecords = totalRecords;
        ValidRecords = validRecords;
        MissingFields = missingFields;
        InvalidRows = invalidRows;
    }

    public int TotalRecords { get; }

    public int ValidRecords { get; }

    public int InvalidRecords => TotalRecords - ValidRecords;

    public IReadOnlyList<string> MissingFields { get; }

    public IReadOnlyList<int> InvalidRows { get; }
}
