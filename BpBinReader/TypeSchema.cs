namespace BpBinReader;
public class TypeSchema(string name, string fullName, IReadOnlyList<FieldSchema> fields, Type type, Guid typeId) {
    public string FullName { get; } = fullName;
    public string Name { get; } = name;
    public IReadOnlyList<FieldSchema> Fields { get; } = fields;
    public Type Type { get; } = type;
    public Guid TypeId = typeId;
}
