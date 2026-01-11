using Kingmaker.Blueprints.JsonSystem.Helpers;
using Newtonsoft.Json;
using Owlcat.Runtime.Core.Utility;
using System.Reflection;
using UnityEngine;

namespace BpBinReader;

public class ReflectionTypeSchemaProvider : ITypeSchemaProvider {
    private readonly Dictionary<Guid, TypeSchema> m_TypeSchemaCache = new();
    private readonly Dictionary<Guid, Type> m_TypeById;

    private readonly Type m_SimpleBlueprintType;
    private readonly Type m_BlueprintComponentType;
    private readonly Type m_ElementType;

    private readonly Type m_SerializeFieldAttributeType;
    private readonly Type m_NonSerializedAttributeType;
    private readonly Type m_SerializableAttribute;

    private readonly Type m_TypeIdAttribute;
    private readonly PropertyInfo m_GuidProp;
#warning make game dependent!
    public ReflectionTypeSchemaProvider() {
        m_SimpleBlueprintType = FindType("Kingmaker.Blueprints.SimpleBlueprint");

        m_BlueprintComponentType = FindType("Kingmaker.Blueprints.BlueprintComponent");

        m_ElementType = FindType("Kingmaker.ElementsSystem.Element");

        m_SerializeFieldAttributeType = FindType("UnityEngine.SerializeField");
        m_NonSerializedAttributeType = typeof(NonSerializedAttribute);
        m_SerializableAttribute = typeof(SerializableAttribute);

        m_TypeIdAttribute = FindType("Kingmaker.Blueprints.JsonSystem.Helpers.TypeIdAttribute");

        m_GuidProp = m_TypeIdAttribute.GetProperty("Guid", BindingFlags.Instance | BindingFlags.Public);

        m_TypeById = BuildTypeIdIndex();
    }

    public TypeSchema Resolve(Guid typeId) {
        if (typeId == Guid.Empty) {
            throw new ArgumentException("TypeId is empty.", nameof(typeId));
        }

        if (!m_TypeById.TryGetValue(typeId, out var type)) {
            throw new KeyNotFoundException($"TypeId {typeId:N} was not found in loaded assemblies.");
        }
        if (!m_TypeSchemaCache.ContainsKey(typeId)) {
            m_TypeSchemaCache[typeId] = BuildSchema(type);
        }
        return m_TypeSchemaCache[typeId];
    }

    private TypeSchema BuildSchema(Type type) {
        var fields = GetUnitySerializedFields(type)
            .Select(f => new FieldSchema(f.Name, BuildValueSchema(f.FieldType)))
            .ToArray();

        return new TypeSchema(type.FullName ?? type.Name, fields);
    }
#warning make game dependent?
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
            return ValueSchema.EnumInt32();
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

        // Complex objects: either “identified” (binary begins with TypeId) or “plain serializable” (no TypeId).
        var isIdentified = IsIdentifiedType(fieldType);

        // For non-identified objects we need a schema directly for that exact type.
        // For identified objects, schema will be chosen at runtime based on the embedded TypeId,
        // but we still need a placeholder TypeSchema to satisfy ValueSchema.ObjectType.
        var schema = new TypeSchema(fieldType.FullName ?? fieldType.Name, Array.Empty<FieldSchema>());

        if (!isIdentified) {
            schema = BuildSchema(fieldType);
        }

        return ValueSchema.Object(schema, isIdentifiedType: isIdentified);
    }
#warning make game dependent!
    private IEnumerable<FieldInfo> GetUnitySerializedFields(Type type) {
        for (var t = type; t != null; t = t.BaseType) {
            foreach (var f in t.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (f.IsPublic || HasAttribute(f, m_SerializeFieldAttributeType)) {
                    if (!HasAttribute(f, m_NonSerializedAttributeType) && IsSerializableType(f, f.FieldType, true)) {
                        yield return f;
                    }
                }
            }
        }
    }
#warning make game dependent!
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