namespace BpBinReader;
public class TypeSchema(string fullName, IReadOnlyList<FieldSchema> fields) {
    public string FullName { get; } = fullName;
    public IReadOnlyList<FieldSchema> Fields { get; } = fields;
}
