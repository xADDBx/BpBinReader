using System.Text;

namespace BpBinReader;

public static class BinaryToJsonConverter {
    public static void DumpBlueprintPackToJson(string packPath, string outputJsonPath, ITypeSchemaProvider schemaProvider) {
        if (!File.Exists(packPath)) {
            throw new FileNotFoundException("Blueprint pack not found.", packPath);
        }

        using var packStream = File.Open(packPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var toc = ReadToc(packStream);

        using var output = new StreamWriter(outputJsonPath, false, new UTF8Encoding(false));

        output.WriteLine("{");
        output.WriteLine("  \"blueprints\": {");

        var first = true;
        foreach (var kvp in toc) {
            var bpGuid = kvp.Key;
            var offset = kvp.Value;
            if (offset == 0) {
                continue;
            }

            packStream.Seek(offset, SeekOrigin.Begin);
            using var reader = new BinaryReader(packStream, Encoding.UTF8, leaveOpen: true);
            var json = new JsonWriter(output);

            if (!first) {
                output.WriteLine(",");
            }
            first = false;

            output.Write($"    \"{bpGuid}\": ");

            var serializer = new BinaryToJsonBlueprintSerializer(reader, schemaProvider);
            serializer.ReadBlueprintAsJson(json);
        }

        output.WriteLine();
        output.WriteLine("  }");
        output.WriteLine("}");
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

    private sealed class BinaryToJsonBlueprintSerializer {
        private readonly BinaryReader _reader;
        private readonly ITypeSchemaProvider m_SchemaProvider;

        public BinaryToJsonBlueprintSerializer(BinaryReader reader, ITypeSchemaProvider schemaProvider) {
            _reader = reader;
            m_SchemaProvider = schemaProvider;
        }

        public void ReadBlueprintAsJson(JsonWriter json) {
            // Equivalent to ReflectionBasedSerializer.Blueprint(ref bp):
            // - GenericObject (identified type => the typeid GUID is stored in the stream)
            // - then name (string) + AssetGuid (string) for the blueprint root
            var rootTypeId = ReadTypeId();
            if (rootTypeId == Guid.Empty) {
                json.WriteNull();
                return;
            }

            var rootSchema = m_SchemaProvider.Resolve(rootTypeId);
            json.BeginObject();

            // Emit type info for debugging / downstream tooling.
            json.WritePropertyName("$typeId");
            json.WriteString(rootTypeId.ToString("N"));

            json.WritePropertyName("$typeName");
            json.WriteString(rootSchema?.FullName);

            json.WritePropertyName("data");
            ReadObjectBodyAsJson(json, rootSchema);

            // Blueprint(ref bp) adds two strings after the object payload
            var name = _reader.ReadString();
            var assetGuid = _reader.ReadString();

            json.WritePropertyName("name");
            json.WriteString(name);

            json.WritePropertyName("assetGuid");
            json.WriteString(assetGuid);

            json.EndObject();
        }

        private void ReadObjectBodyAsJson(JsonWriter json, TypeSchema? schema) {
            if (schema is null) {
                // If schema is missing, we can't advance correctly; hard-fail so corruption is obvious.
                throw new InvalidOperationException("Missing schema for type; cannot decode binary safely.");
            }

            json.BeginObject();

            // The binary contains fields in "unity serialized field" order.
            foreach (var field in schema.Fields) {
                json.WritePropertyName(field.Name);
                ReadValueAsJson(json, field.Value);
            }

            json.EndObject();
        }

        private void ReadValueAsJson(JsonWriter json, ValueSchema value) {
            switch (value.Kind) {
                case ValueKind.Int32:
                    json.WriteNumber(_reader.ReadInt32());
                    return;
                case ValueKind.UInt32:
                    json.WriteNumber(_reader.ReadUInt32());
                    return;
                case ValueKind.Int64:
                    json.WriteNumber(_reader.ReadInt64());
                    return;
                case ValueKind.UInt64:
                    json.WriteNumber(_reader.ReadUInt64());
                    return;
                case ValueKind.Single:
                    json.WriteRawNumber(_reader.ReadSingle());
                    return;
                case ValueKind.Double:
                    json.WriteRawNumber(_reader.ReadDouble());
                    return;
                case ValueKind.Boolean:
                    json.WriteBool(_reader.ReadBoolean());
                    return;
                case ValueKind.String:
                    json.WriteString(_reader.ReadString());
                    return;
                case ValueKind.EnumInt32:
                    json.WriteNumber(_reader.ReadInt32());
                    return;
                case ValueKind.UnityObjectRef: {
                        var id = _reader.ReadInt32();
                        if (id < 0) {
                            json.WriteNull();
                        } else {
                            // BlueprintReferencedAssets index
                            json.WriteNumber(id);
                        }
                        return;
                    }

                case ValueKind.Array: {
                        var len = _reader.ReadInt32();
                        json.BeginArray();
                        for (var i = 0; i < len; i++) {
                            ReadValueAsJson(json, value.Element!);
                        }
                        json.EndArray();
                        return;
                    }

                case ValueKind.List: {
                        var len = _reader.ReadInt32();
                        json.BeginArray();
                        for (var i = 0; i < len; i++) {
                            ReadValueAsJson(json, value.Element!);
                        }
                        json.EndArray();
                        return;
                    }

                case ValueKind.Object: {
                        // Matches ReflectionBasedSerializer.GenericObject in read-mode:
                        // - for "identified types" the stream includes a TypeId at the start of the object
                        // - otherwise type is known from field schema
                        Guid typeId;
                        TypeSchema? actualSchema;

                        if (value.IsIdentifiedType) {
                            typeId = ReadTypeId();
                            if (typeId == Guid.Empty) {
                                json.WriteNull();
                                return;
                            }

                            actualSchema = m_SchemaProvider.Resolve(typeId);
                        } else {
                            typeId = Guid.Empty;
                            actualSchema = value.ObjectType;
                        }

                        json.BeginObject();

                        if (value.IsIdentifiedType) {
                            json.WritePropertyName("$typeId");
                            json.WriteString(typeId.ToString("N"));

                            json.WritePropertyName("$typeName");
                            json.WriteString(actualSchema?.FullName);
                        }

                        json.WritePropertyName("data");
                        ReadObjectBodyAsJson(json, actualSchema);

                        json.EndObject();
                        return;
                    }

                default:
                    throw new NotSupportedException("Unsupported kind: " + value.Kind);
            }
        }

        private Guid ReadTypeId() {
            var bytes = _reader.ReadBytes(16);
            if (bytes.Length != 16) {
                throw new EndOfStreamException("Unexpected EOF while reading TypeId.");
            }

            var g = new Guid(bytes);
            return g == Guid.Empty ? Guid.Empty : g;
        }
    }
}
