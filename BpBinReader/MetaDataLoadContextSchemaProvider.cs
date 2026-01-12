using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Newtonsoft.Json;
using Owlcat.Runtime.Core.Utility;
using System.Reflection;
using UnityEngine;

namespace BpBinReader;
#warning STUB
public class MetaDataLoadContextSchemaProvider : ITypeSchemaProvider {
    private readonly Dictionary<Guid, TypeSchema> m_TypeSchemaCache = new();
    private readonly Dictionary<Guid, Type> m_TypeById;

    private readonly Type m_SimpleBlueprintType;
    private readonly Type m_BlueprintComponentType;
    private readonly Type m_ElementType;

    private readonly Type m_SerializeFieldAttributeType;
    private readonly Type m_NonSerializedAttributeType;
    private readonly Type m_SerializableAttribute;
    private readonly Type m_JsonIgnoreAttribute;
    private readonly Type m_ExcludeFieldFromBuildAttribute;

    private readonly Type m_TypeIdAttribute;
    private readonly PropertyInfo m_GuidProp; 
    public string GetEnumName(TypeSchema enumType, int value) {
        return Enum.GetName(enumType.Type, value);
    }
#warning make game dependant!
    public MetaDataLoadContextSchemaProvider() {
        m_SimpleBlueprintType = FindType("Kingmaker.Blueprints.SimpleBlueprint");

        m_BlueprintComponentType = FindType("Kingmaker.Blueprints.BlueprintComponent");

        m_ElementType = FindType("Kingmaker.ElementsSystem.Element");

        m_SerializeFieldAttributeType = FindType("UnityEngine.SerializeField");
        m_NonSerializedAttributeType = FindType("System.NonSerializedAttribute");
        m_SerializableAttribute = FindType("System.SerializableAttribute");
        m_JsonIgnoreAttribute = FindType("Newtonsoft.Json.JsonIgnoreAttribute");
        m_ExcludeFieldFromBuildAttribute = FindType("Kingmaker.Blueprints.JsonSystem.Helpers.ExcludeFieldFromBuildAttribute");


        m_TypeIdAttribute = FindType("Kingmaker.Blueprints.JsonSystem.Helpers.TypeIdAttribute");

        m_GuidProp = m_TypeIdAttribute.GetProperty("Guid", BindingFlags.Instance | BindingFlags.Public);

        m_TypeById = BuildTypeIdIndex(); 
        Main.Log.Log($"[Schema] Indexed TypeIds: {m_TypeById.Count}");
    }

    public TypeSchema Resolve(Guid typeId) {
        if (typeId == Guid.Empty) {
            throw new ArgumentException("TypeId is empty.", nameof(typeId));
        }

        if (!m_TypeById.TryGetValue(typeId, out var type)) {
            throw new KeyNotFoundException($"TypeId {typeId:N} was not found in loaded assemblies.");
        }
        if (!m_TypeSchemaCache.ContainsKey(typeId)) {
            m_TypeSchemaCache[typeId] = BuildSchema(type, typeId);
        }
        return m_TypeSchemaCache[typeId];
    }

    private TypeSchema BuildSchema(Type type, Guid typeId) {
        var fields = BlueprintFieldsTraaverser_GetUnitySerializedFields(type)
            .Select(f => new FieldSchema(f.Name, BuildValueSchema(f.FieldType)))
            .ToArray();

        return new TypeSchema(type.Name, type.FullName ?? type.Name, fields, type, typeId);
    }
#warning make game dependant?
    private ValueSchema BuildValueSchema(Type fieldType) {
        if (fieldType == typeof(int)) {
            return ValueSchema.Int32();
        }

        if (fieldType == typeof(uint)) {
            return ValueSchema.UInt32();
        }

        if (fieldType == typeof(long)) {
            return ValueSchema.Int64();
        }

        if (fieldType == typeof(ulong)) {
            return ValueSchema.UInt64();
        }

        if (fieldType == typeof(float)) {
            return ValueSchema.Single();
        }

        if (fieldType == typeof(double)) {
            return ValueSchema.Double();
        }

        if (fieldType == typeof(bool)) {
            return ValueSchema.Boolean();
        }

        if (fieldType == typeof(string)) {
            return ValueSchema.String();
        }

        if (fieldType.IsEnum) {
            // The game assumes int-backed enums.
            return ValueSchema.EnumInt32(BuildSchema(fieldType, default));
        }

        if (fieldType == typeof(Color)) {
            return ValueSchema.Color();
        }

        if (fieldType == typeof(Color32)) {
            return ValueSchema.Color32();
        }

        if (fieldType == typeof(Vector2)) {
            return ValueSchema.Vector2();
        }

        if (fieldType == typeof(Vector3)) {
            return ValueSchema.Vector3();
        }

        if (fieldType == typeof(Vector4)) {
            return ValueSchema.Vector4();
        }

        if (fieldType == typeof(Vector2Int)) {
            return ValueSchema.Vector2Int();
        }

        if (fieldType == typeof(Gradient)) {
            return ValueSchema.Gradient();
        }

        if (fieldType == typeof(AnimationCurve)) {
            return ValueSchema.AnimationCurve();
        }

        if (fieldType == typeof(UnityEngine.UI.ColorBlock)) {
            return ValueSchema.ColorBlock();
        }

        if (fieldType.IsArray) {
            var elementType = fieldType.GetElementType() ?? throw new NotSupportedException($"Array element type missing for {fieldType}.");

            return ValueSchema.Array(BuildValueSchema(elementType));
        }

        if (IsGenericList(fieldType)) {
            var elementType = fieldType.GetGenericArguments()[0];
            return ValueSchema.List(BuildValueSchema(elementType));
        }

        if (IsUnityObject(fieldType)) {
            return ValueSchema.UnityObjectRef();
        }

        if (IsBpRef(fieldType)) {
            return ValueSchema.BlueprintRef();
        }

        // Complex objects: either “identified” (binary begins with TypeId) or “plain serializable” (no TypeId).
        var isIdentified = IsIdentifiedType(fieldType);

        // Dummy Schema; will be replaced if identified.
        var schema = new TypeSchema(fieldType.Name, fieldType.FullName ?? fieldType.Name, Array.Empty<FieldSchema>(), fieldType, default);

        if (!isIdentified) {
            schema = BuildSchema(fieldType, default);
        }

        return ValueSchema.Object(schema, isIdentifiedType: isIdentified);
    }
#warning make game depandent!
    private bool IsBpRef(Type t) {
        return typeof(BlueprintReferenceBase).IsAssignableFrom(t);
    }
    private static Dictionary<Type, List<FieldInfo>> m_UnitySerializedFieldsCache = [];
    public IEnumerable<FieldInfo> BlueprintFieldsTraaverser_GetUnitySerializedFields(Type type) {
        List<FieldInfo> list;
        if (!m_UnitySerializedFieldsCache.TryGetValue(type, out list)) {
            list = [.. FieldsContractResolver_GetUnitySerializedFields(type).Where(f => !HasAttribute(f, m_JsonIgnoreAttribute) && !HasAttribute(f, m_ExcludeFieldFromBuildAttribute))];
            m_UnitySerializedFieldsCache[type] = list;
        }
        return list;
    }
    private IEnumerable<FieldInfo> FieldsContractResolver_GetUnitySerializedFields(Type type) {
        IEnumerable<FieldInfo>? baseFields = [];
        if (type.BaseType != null) {
            baseFields = FieldsContractResolver_GetUnitySerializedFields(type.BaseType);
        }
        return baseFields.Concat(type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.IsPublic || HasAttribute(f, m_SerializeFieldAttributeType))
            .Where(f => !HasAttribute(f, m_NonSerializedAttributeType))
            .Where(f => IsSerializableType(f, f.FieldType, true)));
    }
#warning make game depandent!
    private bool IsSerializableType(FieldInfo field, Type fieldType, bool arraysAllowed = true) {
        if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType == typeof(string)) {
            return true;
        }
        if (fieldType.IsOrSubclassOf<global::UnityEngine.Object>()) {
            return true;
        }
        if (fieldType == typeof(object) && field.HasAttribute<ModsPatchSerializableAttribute>()) {
            return true;
        }
        if (fieldType == typeof(AnimationCurve)) {
            return true;
        }
        if (fieldType == typeof(Rect)) {
            return true;
        }
        if (fieldType == typeof(Vector2)) {
            return true;
        }
        if (fieldType == typeof(Vector3)) {
            return true;
        }
        if (fieldType == typeof(Vector4)) {
            return true;
        }
        if (fieldType == typeof(Vector2Int)) {
            return true;
        }
        if (fieldType == typeof(Vector3Int)) {
            return true;
        }
        if (fieldType == typeof(Color)) {
            return true;
        }
        if (fieldType == typeof(Color32)) {
            return true;
        }
        if (fieldType == typeof(Gradient)) {
            return true;
        }
        if (fieldType.IsArray) {
            return arraysAllowed && IsSerializableType(field, fieldType.GetElementType(), false);
        }
        if (fieldType.IsList()) {
            return arraysAllowed && IsSerializableType(field, fieldType.GetGenericArguments()[0], false);
        }
        return HasAttribute(fieldType, m_SerializableAttribute) && fieldType.Assembly != FindType("System.Int32").Assembly;
    }
    private bool IsIdentifiedType(Type t) {
        // Matches ReflectionBasedSerializer.IsIdentifiedType(...)
        return IsSubclassOfOrSame(t, m_SimpleBlueprintType)
            || IsSubclassOfOrSame(t, m_BlueprintComponentType)
            || IsSubclassOfOrSame(t, m_ElementType);
    }

    private static bool IsSubclassOfOrSame(Type t, Type? maybeBase) {
        return maybeBase != null && (t == maybeBase || t.IsSubclassOf(maybeBase));
    }

    private static bool IsGenericList(Type t) {
        return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
    }

    private static bool IsUnityObject(Type t) {
        // UnityEngine.Object is only available in-game.
        var unityObject = FindType("UnityEngine.Object");
        return t == unityObject || t.IsSubclassOf(unityObject);
    }

    private static bool HasAttribute(MemberInfo member, Type? attributeType) {
        if (attributeType == null) {
            return false;
        }

        return member.GetCustomAttributes(attributeType, inherit: true).Any();
    }

    private Dictionary<Guid, Type> BuildTypeIdIndex() {
        var map = new Dictionary<Guid, Type>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            Type[] types;
            try {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex) {
                types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
            }

            foreach (var t in types) {
                if (t == null) {
                    continue;
                }

                var attr = t.GetCustomAttributes(m_TypeIdAttribute, inherit: false).FirstOrDefault();
                if (attr == null) {
                    continue;
                }

                var guidValue = m_GuidProp.GetValue(attr) as Guid?;
                if (guidValue == null || guidValue.Value == Guid.Empty) {
                    continue;
                }

                if (map.ContainsKey(guidValue.Value)) {
                    if (!t.IsAbstract) {
                        throw new InvalidOperationException($"Duplicate TypeId {guidValue.Value:N} found on non abstract! types '{map[guidValue.Value].FullName}' and '{t.FullName}'.");
                    } else {
                        Main.Log.Warning($"Duplicate TypeId {guidValue.Value:N} found on abstract types '{map[guidValue.Value].FullName}' and '{t.FullName}'.");
                    }
                }
                map[guidValue.Value] = t;
            }
        }

        return map;
    }
    private static Dictionary<string, Type> m_TypeCache = new();
    private static Type FindType(string fullName) {
        if (m_TypeCache.TryGetValue(fullName, out var cachedType)) {
            return cachedType;
        }
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            var t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (t != null) {
                m_TypeCache[fullName] = t;
                return t;
            }
        }
        throw new Exception($"Type '{fullName}' not found in loaded assemblies.");
    }
}