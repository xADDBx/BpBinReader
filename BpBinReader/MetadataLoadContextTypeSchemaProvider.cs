using System;
using System.Globalization;
using System.Reflection;

namespace BpBinReader;

public abstract class MetadataLoadContextTypeSchemaProvider : ITypeSchemaProvider, IDisposable {
    protected readonly MetadataLoadContext Mlc;
    protected readonly Dictionary<Guid, TypeSchema> TypeSchemaCache = [];
    protected readonly Dictionary<Guid, Type> TypeById = [];
    protected readonly Dictionary<string, Type> TypeByFullName = []; 
    private readonly Dictionary<(MemberInfo, Type?), (CustomAttributeData?, bool)> m_AttributeCache = [];
    private readonly Dictionary<(Type, Type), bool> m_IsOrSubclassOfCache = [];
    private readonly Dictionary<Type, bool> m_IsListCache = []; 
    protected readonly Type NonSerializedAttributeType;
    protected readonly Type SerializableAttributeType;
    protected readonly Type SerializeFieldAttributeType;
    protected readonly Type UnityObjectType;
    protected readonly Type JsonIgnoreAttributeType;
    private readonly Type m_FlagsAttributeType;
    private readonly Type m_AtributeUsageType;
    private readonly Type m_DelegateType;
    protected abstract Type BlueprintReferenceBaseType { get; }
    public MetadataLoadContextTypeSchemaProvider(IEnumerable<string> assemblyDirectoryPaths) {
        var resolver = new PathAssemblyResolver(assemblyDirectoryPaths.SelectMany(Directory.EnumerateFiles));
        Mlc = new MetadataLoadContext(resolver);

        // Load all requested assemblies into the MLC.
        foreach (var p in assemblyDirectoryPaths.SelectMany(dir => Directory.EnumerateFiles(dir, "*.dll"))) {
            var asm = Mlc.LoadFromAssemblyPath(p);
            foreach (var t in SafeGetTypes(asm)) {
                if (t.FullName != null && !TypeByFullName.ContainsKey(t.FullName)) {
                    TypeByFullName[t.FullName] = t;
                }
            }
        }
        NonSerializedAttributeType = RequireType("System.NonSerializedAttribute");
        SerializableAttributeType = RequireType("System.SerializableAttribute"); 
        SerializeFieldAttributeType = RequireType("UnityEngine.SerializeField");
        UnityObjectType = RequireType("UnityEngine.Object");
        JsonIgnoreAttributeType = RequireType("Newtonsoft.Json.JsonIgnoreAttribute");
        m_FlagsAttributeType = RequireType("System.FlagsAttribute");
        m_AtributeUsageType = RequireType("System.AttributeUsageAttribute");
        m_DelegateType = RequireType("System.Delegate");
    }
    public string GetEnumName(TypeSchema t, object value) {
        var fields = t.Type.GetFields(BindingFlags.Public | BindingFlags.Static);
        var underlyingType = GetRuntimePrimitiveTypeFromMetadataType(Enum.GetUnderlyingType(t.Type));
        object rawValue = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        long convertedValue = Convert.ToInt64(rawValue);
        if (HasAttribute(t.Type, m_FlagsAttributeType)) {
            var matching = fields
                .Where(f => (Convert.ToInt64(f.GetRawConstantValue()) & convertedValue) != 0)
                .Select(f => f.Name);

            return string.Join(" | ", matching);
        } else {
            try {
                return fields.First(f => Convert.ToInt64(f.GetRawConstantValue()) == convertedValue).Name;
            } catch (Exception) {
                Console.WriteLine($"{t.Name}, {value}");
                return "WTF Owlcat";
            }
        }
    }
    public TypeSchema Resolve(Guid typeId) {
        if (typeId == Guid.Empty) {
            throw new ArgumentException("TypeId is empty.", nameof(typeId));
        }

        if (!TypeById.TryGetValue(typeId, out var t)) {
            throw new KeyNotFoundException($"TypeId {typeId:N} was not found in provided assemblies.");
        }

        if (!TypeSchemaCache.TryGetValue(typeId, out var schema)) {
            schema = BuildSchema(t, typeId);
            TypeSchemaCache[typeId] = schema;
        }

        return schema;
    }

    public void Dispose() {
        Mlc.Dispose();
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
        // Console.WriteLine($"{type.FullName}: [{string.Join(", ", fields.Select(f => f.Name))}]");
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

        if (IsOrSubclassOf(fieldType, UnityObjectType)) {
            return ValueSchema.UnityObjectRef();
        }

        if (IsOrSubclassOf(fieldType, BlueprintReferenceBaseType)) {
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
        if (TypeByFullName.TryGetValue(fullName, out var t)) {
            return t;
        }
        return null;
    }
    protected bool IsList(Type t) {
        if (!m_IsListCache.TryGetValue(t, out var result)) {
            if (!t.IsGenericType) {
                return false;
            }

            var def = t.GetGenericTypeDefinition();
            result = def.FullName == "System.Collections.Generic.List`1";
            m_IsListCache[t] = result;
        }
        return result;
    }

    protected bool HasAttribute(MemberInfo member, Type? attributeType) {
        return GetAttribute(member, attributeType, out _);
    }
    protected bool GetAttribute(MemberInfo member, Type? attributeType, out CustomAttributeData? data, bool? inherit = null) {
        var key = (member, attributeType);
        bool result;
        if (m_AttributeCache.TryGetValue(key, out var cached)) {
            data = cached.Item1;
            result = cached.Item2;
        } else {
            result = InternalGetAttribute(member, attributeType, out data, inherit);
            m_AttributeCache[key] = (data, result);
        }
        return result;
    }
    private bool InternalGetAttribute(MemberInfo member, Type? attributeType, out CustomAttributeData? data, bool? inherit = null) {
        data = null;
        if (attributeType == null) {
            data = null;
            return false;
        }
        if (member is Type t) {
            if (attributeType == SerializableAttributeType) {
#pragma warning disable SYSLIB0050 // Type or member is obsolete
                if (t.IsSerializable && !IsOrSubclassOf(t.UnderlyingSystemType, m_DelegateType)) {
                    return true;
                }
#pragma warning restore SYSLIB0050 // Type or member is obsolete
            } else {
                bool inherited = true;
                if (!inherit.HasValue) {
                    GetAttribute(attributeType, m_AtributeUsageType, out var usage, true);
                    if (usage == null) {
                        throw new InvalidOperationException($"AttributeUsageAttribute not found on attribute type '{t.FullName}'.");
                    }
                    foreach (var arg in usage.NamedArguments) {
                        if (arg.MemberName == nameof(AttributeUsageAttribute.Inherited)) {
                            inherited = arg.TypedValue.Value as bool? ?? true;
                            break;
                        }
                    }
                } else {
                    inherited = inherit.Value;
                }
                do {
                    data = FindAttributeType(t.CustomAttributes, attributeType);
                    if (data != null) {
                        return true;
                    }
                    t = t.BaseType!;
                } while (inherited && t != null);
            }
        } else if (member is FieldInfo f && attributeType == NonSerializedAttributeType) {
#pragma warning disable SYSLIB0050 // Type or member is obsolete
            return f.IsNotSerialized;
#pragma warning restore SYSLIB0050 // Type or member is obsolete
        }
        data = FindAttributeType(member.CustomAttributes, attributeType);
        return data != null;
    }
    private CustomAttributeData? FindAttributeType(IEnumerable<CustomAttributeData> attributes, Type attributeType) {
        foreach (var data in attributes) {
            try {
                if (data.AttributeType == attributeType) {
                    return data;
                }
            } // i18n type I think
            catch { }
        }
        return null;
    }
    protected bool IsOrSubclassOf(Type t, Type baseType) {
        var key = (t, baseType);
        if (!m_IsOrSubclassOfCache.TryGetValue(key, out var result)) {
            if (t == baseType) {
                result = true;
            } else {
                result = t.IsSubclassOf(baseType);
            }
            m_IsOrSubclassOfCache[key] = result;
        }
        return result;
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
