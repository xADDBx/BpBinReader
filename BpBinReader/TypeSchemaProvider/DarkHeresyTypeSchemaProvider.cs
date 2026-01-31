namespace BpBinReader;

public sealed class DarkHeresyTypeSchemaProvider : V1MetadataBase {
    private readonly Type m_SerializeReferenceAttributeType;

    public DarkHeresyTypeSchemaProvider(IEnumerable<string> assemblyDirectoryPaths) : base(assemblyDirectoryPaths, "Owlcat.Runtime.Core.Utility.TypeIdAttribute") {
        m_SerializeReferenceAttributeType = RequireType("UnityEngine.SerializeReference");
    }
    protected override TypeSchema BuildSchema(Type type, Guid typeId) {
        var fields = GetUnitySerializedFields(type)
            .Select(f => {
                var candidateType = f.FieldType;
                if (candidateType.IsArray) {
                    candidateType = candidateType.GetElementType();
                } else if (IsList(candidateType)) {
                    candidateType = candidateType.GetGenericArguments()[0];
                }

                var forceNeedsType = HasAttribute(f, m_SerializeReferenceAttributeType) && candidateType != null && HasAttribute(candidateType, TypeIdAttributeType);

                return new FieldSchema(f.Name, BuildValueSchema(f.FieldType, forceNeedsType));
            })
            .ToArray();
        // Console.WriteLine($"{type.FullName}: [{string.Join(", ", fields.Select(f => f.Name))}]");
        return new TypeSchema(type.Name, type.FullName ?? type.Name, fields, type, typeId);
    }
}