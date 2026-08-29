namespace Forge3D.Core.Data.Schema;

public sealed class FieldMappingProfile
{
    private readonly Dictionary<ForgeField, string> _mappings = [];

    public IReadOnlyDictionary<ForgeField, string> Mappings => _mappings;

    public void Map(ForgeField field, string? sourceField)
    {
        if (string.IsNullOrWhiteSpace(sourceField))
        {
            _mappings.Remove(field);
            return;
        }

        _mappings[field] = sourceField;
    }

    public bool TryGetSourceField(ForgeField field, out string sourceField)
    {
        return _mappings.TryGetValue(field, out sourceField!);
    }
}
