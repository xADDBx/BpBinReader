using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BpBinReader;

public static class BinaryToJsonConverter {
    public static void DumpBlueprintPackToJson(string packPath, string outputJsonPath, ITypeSchemaProvider schemaProvider) {
        if (!File.Exists(packPath)) {
            throw new FileNotFoundException("Blueprint pack not found.", packPath);
        }

        using var packStream = File.Open(packPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var toc = ReadToc(packStream);

        using var reader = new BinaryReader(packStream, Encoding.UTF8, leaveOpen: true);
        var options = new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriterWrapper(new Utf8JsonWriter(ms, options));
        writer.WriteStartObject();
        writer.WritePropertyName("blueprints");
        writer.WriteStartArray();
        try {
            foreach (var kvp in toc) {
                var bpGuid = kvp.Key;
                var offset = kvp.Value;
                if (offset == 0) {
                    continue;
                }

                packStream.Seek(offset, SeekOrigin.Begin);

                var serializer = new BinaryToJsonBlueprintSerializer(reader, schemaProvider);
                serializer.ReadBlueprintAsJson(writer);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        } finally {
            writer.Flush();
            File.WriteAllText(outputJsonPath, Encoding.UTF8.GetString(ms.ToArray()));
        }
    }

    private static Dictionary<string, uint> ReadToc(Stream packStream) {
        packStream.Seek(0, SeekOrigin.Begin);

        var toc = new Dictionary<string, uint>();
        using var reader = new BinaryReader(packStream, Encoding.UTF8, leaveOpen: true);

        var guidBytes = new byte[16];
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++) {
            reader.Read(guidBytes, 0, 16);
            var guid = new Guid(guidBytes).ToString("N");
            var offset = reader.ReadUInt32();
            toc[guid] = offset;
        }

        return toc;
    }
}
