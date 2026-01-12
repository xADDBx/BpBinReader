using System.Globalization;
using System.Reflection;

namespace BpBinReader;

public abstract class MetadataLoadContextTypeSchemaProvider : ITypeSchemaProvider, IDisposable {
    protected readonly MetadataLoadContext m_Mlc;
    protected readonly Dictionary<Guid, TypeSchema> m_TypeSchemaCache = [];
    protected readonly Dictionary<Guid, Type> m_TypeById = [];
    protected readonly Dictionary<string, Type> m_TypeByFullName = [];
    protected abstract Type m_UnityObjectType { get; }
    protected abstract Type m_BlueprintReferenceBaseType { get; }
    public MetadataLoadContextTypeSchemaProvider(IEnumerable<string> assemblyDirectoryPaths) {
        var resolver = new PathAssemblyResolver(assemblyDirectoryPaths.SelectMany(Directory.EnumerateFiles));
        m_Mlc = new MetadataLoadContext(resolver);

        // Load all requested assemblies into the MLC.
        foreach (var p in assemblyDirectoryPaths.SelectMany(dir => Directory.EnumerateFiles(dir, "*.dll"))) {
            var asm = m_Mlc.LoadFromAssemblyPath(p);
            foreach (var t in SafeGetTypes(asm)) {
                if (t.FullName != null && !m_TypeByFullName.ContainsKey(t.FullName)) {
                    m_TypeByFullName[t.FullName] = t;
                }
            }
        }

    }
    public string GetEnumName(TypeSchema t, object value) {
        var fields = t.Type.GetFields(BindingFlags.Public | BindingFlags.Static);
        var underlyingType = GetRuntimePrimitiveTypeFromMetadataType(Enum.GetUnderlyingType(t.Type));
        object rawValue = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        long convertedValue = Convert.ToInt64(rawValue);
        var matching = fields
            .Where(f => (Convert.ToInt64(f.GetRawConstantValue()) & convertedValue) != 0)
            .Select(f => f.Name);

        return string.Join(" | ", matching);
    }
    public TypeSchema Resolve(Guid typeId) {
        if (typeId == Guid.Empty) {
            throw new ArgumentException("TypeId is empty.", nameof(typeId));
        }

        if (!m_TypeById.TryGetValue(typeId, out var t)) {
            throw new KeyNotFoundException($"TypeId {typeId:N} was not found in provided assemblies.");
        }

        if (!m_TypeSchemaCache.TryGetValue(typeId, out var schema)) {
            schema = BuildSchema(t, typeId);
            m_TypeSchemaCache[typeId] = schema;
        }

        return schema;
    }

    public void Dispose() {
        m_Mlc.Dispose();
    }
    #region GameSpecific
    // Re-implement ReflectionBasedSerializer.GenericObject call chain
    private static Dictionary<Type, IEnumerable<FieldInfo>> s_UnitySerializedFieldsCache = [];
    protected IEnumerable<FieldInfo> GetUnitySerializedFields(Type type) {
        if (!s_UnitySerializedFieldsCache.TryGetValue(type, out var fields)) {
            fields = InternalGetUnitySerializedFields(type);
            s_UnitySerializedFieldsCache[type] = fields;
        }
        return fields;
    }
    protected abstract IEnumerable<FieldInfo> InternalGetUnitySerializedFields(Type type);
    // ReflectionBasedSerializer.IsIdentifiedType
    protected abstract bool IsIdentifiedType(Type type);
    #endregion
    #region SchemaBuilderBase
    protected TypeSchema BuildSchema(Type type, Guid typeId) {
        var fields = GetUnitySerializedFields(type)
            .Select(f => new FieldSchema(f.Name, BuildValueSchema(f.FieldType)))
            .ToArray();

        return new TypeSchema(type.Name, type.FullName ?? type.Name, fields, type, typeId);
    }
    protected ValueSchema BuildValueSchema(Type fieldType) {
        var fn = fieldType.FullName ?? fieldType.Name;

        if (fn == "System.Int32") {
            return ValueSchema.Int32();
        }
        if (fn == "System.UInt32") {
            return ValueSchema.UInt32();
        }
        if (fn == "System.Int64") {
            return ValueSchema.Int64();
        }
        if (fn == "System.UInt64") {
            return ValueSchema.UInt64();
        }
        if (fn == "System.Single") {
            return ValueSchema.Single();
        }
        if (fn == "System.Double") {
            return ValueSchema.Double();
        }
        if (fn == "System.Boolean") {
            return ValueSchema.Boolean();
        }
        if (fn == "System.String") {
            return ValueSchema.String();
        }

        if (fieldType.IsEnum) {
            // Produce a TypeSchema for the enum so GetEnumName can map numeric->name using metadata.
            var enumSchema = BuildSchema(fieldType, default);
            return ValueSchema.EnumInt32(enumSchema);
        }

        // Unity special structs by name.
        switch (fn) {
            case "UnityEngine.Color":
                return ValueSchema.Color();
            case "UnityEngine.Color32":
                return ValueSchema.Color32();
            case "UnityEngine.Vector2":
                return ValueSchema.Vector2();
            case "UnityEngine.Vector3":
                return ValueSchema.Vector3();
            case "UnityEngine.Vector4":
                return ValueSchema.Vector4();
            case "UnityEngine.Vector2Int":
                return ValueSchema.Vector2Int();
            case "UnityEngine.Gradient":
                return ValueSchema.Gradient();
            case "UnityEngine.AnimationCurve":
                return ValueSchema.AnimationCurve();
            case "UnityEngine.UI.ColorBlock":
                return ValueSchema.ColorBlock();
        }

        if (fieldType.IsArray) {
            var elementType = fieldType.GetElementType()
                ?? throw new NotSupportedException($"Array element type missing for {fn}.");

            return ValueSchema.Array(BuildValueSchema(elementType));
        }

        if (IsList(fieldType)) {
            var elementType = fieldType.GetGenericArguments()[0];
            return ValueSchema.List(BuildValueSchema(elementType));
        }

        if (IsOrSubclassOf(fieldType, m_UnityObjectType)) {
            return ValueSchema.UnityObjectRef();
        }

        if (IsOrSubclassOf(fieldType, m_BlueprintReferenceBaseType)) {
            return ValueSchema.BlueprintRef();
        }

        var isIdentified = IsIdentifiedType(fieldType);

        var schema = new TypeSchema(fieldType.Name, fieldType.FullName ?? fieldType.Name, [], typeof(object), default);
        if (!isIdentified) {
            schema = BuildSchema(fieldType, default);
        }

        return ValueSchema.Object(schema, isIdentifiedType: isIdentified);
    }
    #endregion
    #region ReflectionHelper
    protected Type RequireType(string fullName) {
        var t = TryGetType(fullName);
        if (t == null) {
            throw new InvalidOperationException($"Type '{fullName}' not found in supplied MetadataLoadContext assemblies.");
        }
        return t;
    }

    private Type? TryGetType(string fullName) {
        if (m_TypeByFullName.TryGetValue(fullName, out var t)) {
            return t;
        }
        return null;
    }
    protected static bool IsList(Type t) {
        if (!t.IsGenericType) {
            return false;
        }

        var def = t.GetGenericTypeDefinition();
        return def.FullName == "System.Collections.Generic.List`1";
    }

    protected static bool HasAttribute(MemberInfo member, Type? attributeType) {
        return GetAttribute(member, attributeType) != null;
    }

    protected static CustomAttributeData? GetAttribute(MemberInfo member, Type? attributeType) {
        if (attributeType == null) {
            return null;
        }
        foreach (var data in member.CustomAttributes) {
            try {
                if (data.AttributeType.FullName == attributeType.FullName) {
                    return data;
                }
            } // i18n type I think
            catch { }
        }
        return null;
    }

    protected static bool IsOrSubclassOf(Type t, Type baseType) {
        if (t == baseType) {
            return true;
        }

        return t.IsSubclassOf(baseType);
    }
    protected static IEnumerable<Type> SafeGetTypes(Assembly asm) {
        try {
            return asm.GetTypes();
        } catch (ReflectionTypeLoadException ex) {
            return ex.Types.Where(t => t != null).Cast<Type>().ToArray();
        }
    }
    private static Type GetRuntimePrimitiveTypeFromMetadataType(Type t) {
        return t.FullName switch {
            "System.Int32" => typeof(int),
            "System.UInt32" => typeof(uint),
            "System.Int64" => typeof(long),
            "System.UInt64" => typeof(ulong),
            "System.Byte" => typeof(byte),
            "System.SByte" => typeof(sbyte),
            "System.Int16" => typeof(short),
            "System.UInt16" => typeof(ushort),
            _ => throw new NotSupportedException($"Unsupported underlying enum type '{t.FullName}'")
        };
    }
    #endregion
}
