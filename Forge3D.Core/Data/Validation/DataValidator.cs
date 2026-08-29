using Forge3D.Core.Data.Schema;

namespace Forge3D.Core.Data.Validation;

public sealed class DataValidator
{
    public DataValidationResult Validate(ParsedDataSet dataSet, FieldMappingProfile mapping, IEnumerable<ForgeField> requiredFields)
    {
        var missingFields = requiredFields
            .Where(field => !mapping.TryGetSourceField(field, out var sourceField)
                || dataSet.Records.All(record => !record.Fields.ContainsKey(sourceField)))
            .Select(field => field.ToString())
            .ToList();

        var invalidRows = new List<int>();
        for (var i = 0; i < dataSet.Records.Count; i++)
        {
            foreach (var field in requiredFields)
            {
                if (!mapping.TryGetSourceField(field, out var sourceField)
                    || !dataSet.Records[i].Fields.TryGetValue(sourceField, out var value)
                    || string.IsNullOrWhiteSpace(value))
                {
                    invalidRows.Add(i + 1);
                    break;
                }
            }
        }

        return new DataValidationResult(dataSet.Records.Count, dataSet.Records.Count - invalidRows.Count, missingFields, invalidRows);
    }
}
