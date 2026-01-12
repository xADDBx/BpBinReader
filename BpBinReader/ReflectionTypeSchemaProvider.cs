using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.BinaryFormat;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Owlcat.Runtime.Core.Utility;
using System.Reflection;
using UnityEngine;

namespace BpBinReader;

public class ReflectionTypeSchemaProvider : ITypeSchemaProvider {
    private readonly Dictionary<Guid, TypeSchema> m_TypeSchemaCache = new();
    private readonly Dictionary<Guid, Type> m_TypeById;

    private readonly Type m_TypeIdAttribute = typeof(TypeIdAttribute);
    private readonly PropertyInfo m_GuidProp = typeof(TypeIdAttribute).GetProperty("Guid", BindingFlags.Instance | BindingFlags.Public);
    public ReflectionTypeSchemaProvider() {
        m_TypeById = BuildTypeIdIndex(); 
        Main.Log.Log($"[Schema] Indexed TypeIds: {m_TypeById.Count}");
    }

    public string GetEnumName(TypeSchema enumType, int value) {
        return Enum.GetName(enumType.Type, value);
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
        var fields = BlueprintFieldsTraverser.GetUnitySerializedFields(type).Cast<FieldInfo>()
            .Select(f => new FieldSchema(f.Name, BuildValueSchema(f.FieldType)))
            .ToArray();

        return new TypeSchema(type.Name, type.FullName ?? type.Name, fields, type, typeId);
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

        if (fieldType.IsList()) {
            var elementType = fieldType.GetGenericArguments()[0];
            return ValueSchema.List(BuildValueSchema(elementType));
        }

        if (fieldType.IsOrSubclassOf<UnityEngine.Object>()) {
            return ValueSchema.UnityObjectRef();
        }

        if (fieldType.IsOrSubclassOf<BlueprintReferenceBase>()) {
            return ValueSchema.BlueprintRef();
        }

        // Complex objects: either “identified” (binary begins with TypeId) or “plain serializable” (no TypeId).
        var isIdentified = ReflectionBasedSerializer.IsIdentifiedType(fieldType);

        // Dummy Schema, will be replaced if identified.
        var schema = new TypeSchema(fieldType.Name, fieldType.FullName ?? fieldType.Name, Array.Empty<FieldSchema>(), fieldType, default);

        if (!isIdentified) {
            schema = BuildSchema(fieldType, default);
        }

        return ValueSchema.Object(schema, isIdentifiedType: isIdentified);
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
}