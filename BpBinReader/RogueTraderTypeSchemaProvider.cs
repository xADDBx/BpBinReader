using System.Reflection;

namespace BpBinReader;

public sealed class RogueTraderTypeSchemaProvider : MetadataLoadContextTypeSchemaProvider {
    private readonly Type m_TypeIdAttributeType;
    private readonly Type m_ExcludeFieldFromBuildAttributeType;
    private readonly Type m_ModsPatchSerializableAttributeType;
    private readonly Type m_SimpleBlueprintType;
    private readonly Type m_BlueprintComponentType;
    private readonly Type m_ElementType;
    protected override Type BlueprintReferenceBaseType { get; }

    public RogueTraderTypeSchemaProvider(IEnumerable<string> assemblyDirectoryPaths) : base(assemblyDirectoryPaths) {
        m_TypeIdAttributeType = RequireType("Kingmaker.Blueprints.JsonSystem.Helpers.TypeIdAttribute");
        m_ExcludeFieldFromBuildAttributeType = RequireType("Kingmaker.Blueprints.JsonSystem.Helpers.ExcludeFieldFromBuildAttribute");
        m_ModsPatchSerializableAttributeType = RequireType("Kingmaker.Blueprints.JsonSystem.Helpers.ModsPatchSerializableAttribute");
        BlueprintReferenceBaseType = RequireType("Kingmaker.Blueprints.BlueprintReferenceBase");
        m_SimpleBlueprintType = RequireType("Kingmaker.Blueprints.SimpleBlueprint");
        m_BlueprintComponentType = RequireType("Kingmaker.Blueprints.BlueprintComponent");
        m_ElementType = RequireType("Kingmaker.ElementsSystem.Element");

        foreach (var t in TypeByFullName.Values) {
            GetAttribute(t, m_TypeIdAttributeType, out var attr);
            if (attr != null) {
                string? guid = attr.ConstructorArguments[0].Value as string;
                if (string.IsNullOrWhiteSpace(guid)) {
                    continue;
                }

                TypeById[new(guid)] = t;
            }
        }
    }

    protected override IEnumerable<FieldInfo> InternalGetUnitySerializedFields(Type type) {
        return FieldsContractResolver_GetUnitySerializedFields(type)
            .Where(f => !HasAttribute(f, JsonIgnoreAttributeType))
            .Where(f => !HasAttribute(f, m_ExcludeFieldFromBuildAttributeType));
    }
    private IEnumerable<FieldInfo> FieldsContractResolver_GetUnitySerializedFields(Type type) {
        List<FieldInfo> allFields = [];
        if (type.BaseType != null) {
            allFields.AddRange(FieldsContractResolver_GetUnitySerializedFields(type.BaseType));
        }
        allFields.AddRange(type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.IsPublic || HasAttribute(f, SerializeFieldAttributeType))
            .Where(f => !HasAttribute(f, NonSerializedAttributeType))
            .Where(f => FieldsContractResolver_IsSerializableType(f, f.FieldType, true)));
        return allFields;
    }
    private bool FieldsContractResolver_IsSerializableType(FieldInfo field, Type? fieldType, bool arraysAllowed) {
        if (fieldType == null) {
            return false;
        }
        var fn = fieldType.FullName ?? fieldType.Name;

        if (fieldType.IsPrimitive || fieldType.IsEnum || fn == "System.String") {
            return true;
        }

        if (IsOrSubclassOf(fieldType, UnityObjectType)) {
            return true;
        }

        if (fn == "System.Object" && HasAttribute(field, m_ModsPatchSerializableAttributeType)) {
            return true;
        }

        if (fn == "UnityEngine.AnimationCurve"
            || fn == "UnityEngine.Rect"
            || fn == "UnityEngine.Vector2"
            || fn == "UnityEngine.Vector3"
            || fn == "UnityEngine.Vector4"
            || fn == "UnityEngine.Vector2Int"
            || fn == "UnityEngine.Vector3Int"
            || fn == "UnityEngine.Color"
            || fn == "UnityEngine.Color32"
            || fn == "UnityEngine.Gradient") {
            return true;
        }

        if (fieldType.IsArray) {
            if (!arraysAllowed) {
                return false;
            }
            return FieldsContractResolver_IsSerializableType(field, fieldType.GetElementType(), false);
        }

        if (IsList(fieldType)) {
            if (!arraysAllowed) {
                return false;
            }
            return FieldsContractResolver_IsSerializableType(field, fieldType.GetGenericArguments()[0], false);
        }

        if (HasAttribute(fieldType, SerializableAttributeType)) {
            var asmName = fieldType.Assembly.GetName().Name;
            return !string.Equals(asmName, "mscorlib", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    protected override bool IsIdentifiedType(Type t) {
        return IsOrSubclassOf(t, m_SimpleBlueprintType) || IsOrSubclassOf(t, m_BlueprintComponentType) || IsOrSubclassOf(t, m_ElementType);
    }
}