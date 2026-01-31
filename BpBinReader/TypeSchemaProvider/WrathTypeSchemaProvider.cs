using System.Reflection;

namespace BpBinReader;

public sealed class WrathTypeSchemaProvider : V1MetadataBase {
    private readonly Type m_BlueprintGuidType;
    private readonly Type m_BoundsType;
    public WrathTypeSchemaProvider(IEnumerable<string> assemblyDirectoryPaths) : base(assemblyDirectoryPaths, "Kingmaker.Blueprints.JsonSystem.TypeIdAttribute") {
        m_BlueprintGuidType = RequireType("Kingmaker.Blueprints.BlueprintGuid");
        m_BoundsType = RequireType("UnityEngine.Bounds");
    }
    protected override bool RequireModsPatchSerializableAttributeType => false;
    protected override string ExcludeFromBuildAttributeTypeName => "Kingmaker.Blueprints.JsonSystem.BinaryFormat.ExcludeFieldFromBuildAttribute";
    public override bool UseStringAssetIdType => false;
    public override bool SerializedFieldName => true;
    protected override ValueSchema BuildValueSchema(Type fieldType, bool forceNeedsType = false) {
        // Handle specialized Wrath stuff, then fall back to defaults
        if (fieldType == m_BlueprintGuidType) {
            return ValueSchema.BlueprintGuid();
        }
        if (fieldType == m_BoundsType) {
            return ValueSchema.Bounds();
        }
        if (IsOrSubclassOf(fieldType, BlueprintReferenceBaseType)) {
            return ValueSchema.BlueprintRefWrath();
        }
        return base.BuildValueSchema(fieldType, forceNeedsType);
    }
    protected override IEnumerable<FieldInfo> FieldsContractResolver_GetUnitySerializedFields(Type type) {
        List<FieldInfo> allFields = [];
        if (type.BaseType != null) {
            allFields.AddRange(FieldsContractResolver_GetUnitySerializedFields(type.BaseType));
        }
        allFields.AddRange(type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.IsPublic || HasAttribute(f, SerializeFieldAttributeType) || HasAttribute(f, SerializeReferenceAttributeType))
            .Where(f => !HasAttribute(f, NonSerializedAttributeType))
            .Where(f => FieldsContractResolver_IsSerializableType(f, f.FieldType, true)));
        return allFields;
    }
    protected override bool FieldsContractResolver_IsSerializableType(FieldInfo field, Type? fieldType, bool arraysAllowed) {
        // Handle specialized Wrath stuff, thenf all back to defaults
        if (fieldType == null) {
            return false;
        }
        var fn = fieldType.FullName ?? fieldType.Name;
        if (fn == "UnityEngine.Bounds") {
            return true;
        }
        return base.FieldsContractResolver_IsSerializableType(field, fieldType, arraysAllowed);
    }
}
