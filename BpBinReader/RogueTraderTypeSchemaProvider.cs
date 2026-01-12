using System.Reflection;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BpBinReader;

public sealed class RogueTraderTypeSchemaProvider : MetadataLoadContextTypeSchemaProvider {
    private readonly Type m_TypeIdAttributeType;
    private readonly PropertyInfo m_TypeIdGuidProp;

    private readonly Type m_ExcludeFieldFromBuildAttributeType;
    private readonly Type m_JsonIgnoreAttributeType;
    private readonly Type m_SerializeFieldAttributeType;
    private readonly Type m_NonSerializedAttributeType;
    private readonly Type m_SerializableAttributeType;
    private readonly Type m_ModsPatchSerializableAttributeType;

    protected override Type m_UnityObjectType { get; }
    protected override Type m_BlueprintReferenceBaseType { get; }

    private readonly Type m_SimpleBlueprintType;
    private readonly Type m_BlueprintComponentType;
    private readonly Type m_ElementType;

    public RogueTraderTypeSchemaProvider(IEnumerable<string> assemblyDirectoryPaths) : base(assemblyDirectoryPaths) {
        m_TypeIdAttributeType = RequireType("Kingmaker.Blueprints.JsonSystem.Helpers.TypeIdAttribute");
        m_TypeIdGuidProp = m_TypeIdAttributeType.GetProperty("Guid", BindingFlags.Instance | BindingFlags.Public) ?? throw new InvalidOperationException("TypeIdAttribute.Guid property not found.");


        m_ExcludeFieldFromBuildAttributeType = RequireType("Kingmaker.Blueprints.JsonSystem.Helpers.ExcludeFieldFromBuildAttribute");
        m_JsonIgnoreAttributeType = RequireType("Newtonsoft.Json.JsonIgnoreAttribute");
        m_ModsPatchSerializableAttributeType = RequireType("Kingmaker.Blueprints.JsonSystem.Helpers.ModsPatchSerializableAttribute");
        m_SerializeFieldAttributeType = RequireType("UnityEngine.SerializeField");
        m_NonSerializedAttributeType = RequireType("System.NonSerializedAttribute");
        m_SerializableAttributeType = RequireType("System.SerializableAttribute");

        m_UnityObjectType = RequireType("UnityEngine.Object");
        m_BlueprintReferenceBaseType = RequireType("Kingmaker.Blueprints.BlueprintReferenceBase");

        m_SimpleBlueprintType = RequireType("Kingmaker.Blueprints.SimpleBlueprint");
        m_BlueprintComponentType = RequireType("Kingmaker.Blueprints.BlueprintComponent");
        m_ElementType = RequireType("Kingmaker.ElementsSystem.Element");

        foreach (var asm in m_TypeByFullName.Values.Select(t => t.Assembly).Distinct()) {
            foreach (var t in SafeGetTypes(asm)) {
                var attr = GetAttribute(t, m_TypeIdAttributeType);
                if (attr != null) {

                    string? guid = attr.ConstructorArguments[0].Value as string;
                    if (string.IsNullOrWhiteSpace(guid)) {
                        continue;
                    }

                    m_TypeById[new(guid)] = t;
                }
            }
        }
    }

    protected override IEnumerable<FieldInfo> InternalGetUnitySerializedFields(Type type) {
        return FieldsContractResolver_GetUnitySerializedFields(type)
            .Where(f => !HasAttribute(f, m_JsonIgnoreAttributeType))
            .Where(f => !HasAttribute(f, m_ExcludeFieldFromBuildAttributeType));
    }
    private IEnumerable<FieldInfo> FieldsContractResolver_GetUnitySerializedFields(Type type) {
        List<FieldInfo> allFields = [];
        if (type.BaseType != null) {
            allFields.AddRange(FieldsContractResolver_GetUnitySerializedFields(type.BaseType));
        }
        allFields.AddRange(type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.IsPublic || HasAttribute(f, m_SerializeFieldAttributeType))
            .Where(f => !HasAttribute(f, m_NonSerializedAttributeType))
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

        if (IsOrSubclassOf(fieldType, m_UnityObjectType)) {
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

        if (HasAttribute(fieldType, m_SerializableAttributeType)) {
            var asmName = fieldType.Assembly.GetName().Name;
            Console.WriteLine($"{asmName}");
            return !string.Equals(asmName, "mscorlib", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    protected override bool IsIdentifiedType(Type t) {
        return IsOrSubclassOf(t, m_SimpleBlueprintType) || IsOrSubclassOf(t, m_BlueprintComponentType) || IsOrSubclassOf(t, m_ElementType);
    }
}