using System.Text.Json;

namespace BpBinReader;

public sealed class DarkHeresyTypeSchemaProvider : V1MetadataBase {
    private readonly Type m_LocalizedStringType;
    private readonly Dictionary<string, string> m_Strings;
    public DarkHeresyTypeSchemaProvider(IEnumerable<string> assemblyDirectoryPaths) : base(assemblyDirectoryPaths, "Owlcat.Runtime.Core.Utility.TypeIdAttribute") {
        m_LocalizedStringType = RequireType("Kingmaker.Localization.LocalizedString");
        var path = Path.Combine(assemblyDirectoryPaths.First(), "..", "StreamingAssets", "Localization", "enGB.json");
        var template = new {
            strings = new Dictionary<string, JsonElement>()
        };
        // Don't want to ship Newtonsoft
        static T DeserializeAnonymousType<T>(string json, T anonymousTypeObject) => JsonSerializer.Deserialize<T>(json)!;
        var root = DeserializeAnonymousType(File.ReadAllText(path), template);

        m_Strings = root.strings.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetProperty("Text").GetString()!
        );
    }

    protected override ValueSchema BuildValueSchema(Type fieldType, bool forceNeedsType = false) {
        // Handle specialized Wrath stuff, then fall back to defaults
        if (fieldType == m_LocalizedStringType) {
            return ValueSchema.LocalizedString();
        }
        return base.BuildValueSchema(fieldType, forceNeedsType);
    }
    public override string GetLocalizedStringText(string key) {
        if (m_Strings.TryGetValue(key, out var result)) {
            return result;
        } else {
            return "!STALE!";
        }
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

                var forceNeedsType = HasAttribute(f, SerializeReferenceAttributeType) && candidateType != null && HasAttribute(candidateType, TypeIdAttributeType);

                return new FieldSchema(f.Name, BuildValueSchema(f.FieldType, forceNeedsType));
            })
            .ToArray();
        // Console.WriteLine($"{type.FullName}: [{string.Join(", ", fields.Select(f => f.Name))}]");
        return new TypeSchema(type.Name, type.FullName ?? type.Name, fields, type, typeId);
    }
}