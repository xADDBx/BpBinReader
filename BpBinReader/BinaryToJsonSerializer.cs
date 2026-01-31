namespace BpBinReader;

public class BinaryToJsonBlueprintSerializer(BinaryReader reader, ITypeSchemaProvider schemaProvider, AssetProvider assetProvider) {
    private readonly BinaryReader m_Reader = reader;
    private readonly ITypeSchemaProvider m_SchemaProvider = schemaProvider;
    private readonly AssetProvider m_AssetProvider = assetProvider;
    private readonly byte[] m_TypeIdBuffer = new byte[16];

    public void ReadBlueprintAsJson(Utf8JsonWriterWrapper writer) {
        var rootTypeId = ReadTypeId();
        if (rootTypeId == Guid.Empty) {
            writer.WriteNullValue();
            return;
        }

        var rootSchema = m_SchemaProvider.Resolve(rootTypeId);
        writer.WriteStartObject();

        // writer.WriteString("$typeName", rootSchema?.FullName);

        writer.WritePropertyName("Data");
        ReadObjectBodyAsJson(writer, rootSchema, true);

        var name = m_Reader.ReadString();
        if (m_SchemaProvider.UseStringAssetIdType) {
            writer.WriteString("AssetId", m_Reader.ReadString());
        } else {
            m_Reader.Read(m_TypeIdBuffer, 0, 16);
            writer.WriteString("AssetId", new Guid(m_TypeIdBuffer).ToString("N"));
        }
        writer.WriteString("Name", name);


        writer.WriteEndObject();
    }

    private void ReadObjectBodyAsJson(Utf8JsonWriterWrapper writer, TypeSchema? schema, bool isIdentifiedType) {
        if (schema is null) {
            throw new InvalidOperationException("Missing schema for type; cannot decode binary safely.");
        }

        writer.WriteStartObject();
        if (isIdentifiedType) {
            writer.WriteString("$type", schema.TypeId.ToString("N") + ", " + schema?.Name ?? schema?.FullName ?? "");
        }

        foreach (var field in schema!.SerializedFields) {
            writer.WritePropertyName(field.Name);
            var startPos = m_Reader.BaseStream.Position;
            try {
                if (m_SchemaProvider.SerializedFieldName) {
                    var fieldName = m_Reader.ReadString();
                    if (fieldName != field.Name) {
                        throw new InvalidDataException($"Field name mismatch. Expected '{field.Name}', next serialized is '{fieldName}'\nSerialized fields: {string.Join(", ", schema.SerializedFields.Select(s => s.Name))}");
                    }
                }
                ReadValueAsJson(writer, field.Value);
            } catch (Exception ex) {
                Console.WriteLine($"[Deserialize] Failure at {schema.FullName}.{field.Name} kind={field.Value.Kind} pos=0x{startPos:X} ex={ex}");
                throw;
            }
        }

        writer.WriteEndObject();
    }

    private void ReadValueAsJson(Utf8JsonWriterWrapper writer, ValueSchema value) {
        switch (value.Kind) {
            case ValueKind.Int32:
                writer.WriteNumberValue(m_Reader.ReadInt32());
                return;
            case ValueKind.UInt32:
                writer.WriteNumberValue(m_Reader.ReadUInt32());
                return;
            case ValueKind.Int64:
                writer.WriteNumberValue(m_Reader.ReadInt64());
                return;
            case ValueKind.UInt64:
                writer.WriteNumberValue(m_Reader.ReadUInt64());
                return;
            case ValueKind.Single:
                writer.WriteNumberValue(m_Reader.ReadSingle());
                return;
            case ValueKind.Double:
                writer.WriteNumberValue(m_Reader.ReadDouble());
                return;
            case ValueKind.Boolean:
                writer.WriteBooleanValue(m_Reader.ReadBoolean());
                return;
            case ValueKind.String:
                writer.WriteStringValue(m_Reader.ReadString());
                return;
            case ValueKind.EnumInt32:
                if (m_SchemaProvider.GetEnumName(value.ObjectType!, m_Reader.ReadInt32(), out var repr)) {
                    writer.WriteStringValue((string)repr);
                } else {
                    writer.WriteNumberValue((long)repr);
                }
                return;

            case ValueKind.WeakResourceLink: {
                    if (m_SchemaProvider.SerializedFieldName) {
                        var fieldName = m_Reader.ReadString();
                        if (fieldName != "AssetId") {
                            throw new InvalidOperationException("Trying to read WeakResourceLink but next field is not AssetId");
                        }
                    }
                    var assetId = m_Reader.ReadString();
                    if (string.IsNullOrWhiteSpace(assetId)) {
                        writer.WriteNullValue();
                    } else {
                        writer.WriteStartObject();
                        writer.WriteString("AssetId", assetId);
                        writer.WriteEndObject();
                    }
                    return;
                }

            case ValueKind.UnityObjectRef: {
                    var id = m_Reader.ReadInt32();
                    if (id < 0) {
                        writer.WriteNullValue();
                    } else {
                        writer.WriteStartObject();
                        var ids = m_AssetProvider.GetEntryAtIndex(id);
                        writer.WriteString("AssetId", ids.AssetId);
                        writer.WriteNumber("FileId", ids.FileId);
                        writer.WriteNumber("Index", id);
                        writer.WriteEndObject();
                    }
                    return;
                }

            case ValueKind.BlueprintRef: {
                    var guid = m_Reader.ReadString();
                    if (string.IsNullOrEmpty(guid)) {
                        writer.WriteNullValue();
                    } else {
                        writer.WriteStringValue("!bp_" + guid);
                    }
                    return;
                }

            case ValueKind.BlueprintRefWrath: {
                    m_Reader.Read(m_TypeIdBuffer, 0, 16);
                    var guid = new Guid(m_TypeIdBuffer);
                    if (guid == Guid.Empty) {
                        writer.WriteNullValue();
                    } else {
                        writer.WriteStringValue("!bp_" + guid.ToString("N"));
                    }
                    return;
                }

            case ValueKind.BlueprintGuid: {
                    m_Reader.Read(m_TypeIdBuffer, 0, 16);
                    var guid = new Guid(m_TypeIdBuffer);
                    writer.WriteStringValue("!bp_" + guid.ToString("N"));
                    return;
                }

            case ValueKind.Bounds: {
                    writer.WriteStartObject();
                    var x1 = m_Reader.ReadSingle();
                    var y1 = m_Reader.ReadSingle();
                    var z1 = m_Reader.ReadSingle();
                    var x2 = m_Reader.ReadSingle();
                    var y2 = m_Reader.ReadSingle();
                    var z2 = m_Reader.ReadSingle();
                    writer.WritePropertyName("center");
                    writer.WriteStartObject();
                    writer.WriteNumber("x", x1);
                    writer.WriteNumber("y", y1);
                    writer.WriteNumber("z", z1);
                    writer.WriteEndObject();
                    writer.WritePropertyName("size");
                    writer.WriteStartObject();
                    writer.WriteNumber("x", x2);
                    writer.WriteNumber("y", y2);
                    writer.WriteNumber("z", z2);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.Color: {
                    var r = m_Reader.ReadSingle();
                    var g = m_Reader.ReadSingle();
                    var b = m_Reader.ReadSingle();
                    var a = m_Reader.ReadSingle();

                    writer.WriteStartObject();
                    writer.WriteNumber("r", r);
                    writer.WriteNumber("g", g);
                    writer.WriteNumber("b", b);
                    writer.WriteNumber("a", a);
                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.Color32: {
                    var packed = m_Reader.ReadInt32();

                    var r = (packed & 255);
                    var g = ((packed >> 8) & 255);
                    var b = ((packed >> 16) & 255);
                    var a = ((packed >> 24) & 255);

                    writer.WriteStartObject();
                    writer.WriteNumber("r", r);
                    writer.WriteNumber("g", g);
                    writer.WriteNumber("b", b);
                    writer.WriteNumber("a", a);
                    writer.WriteNumber("packed", packed);
                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.Vector2: {
                    var x = m_Reader.ReadSingle();
                    var y = m_Reader.ReadSingle();

                    writer.WriteStartObject();
                    writer.WriteNumber("x", x);
                    writer.WriteNumber("y", y);
                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.Vector3: {
                    var x = m_Reader.ReadSingle();
                    var y = m_Reader.ReadSingle();
                    var z = m_Reader.ReadSingle();

                    writer.WriteStartObject();
                    writer.WriteNumber("x", x);
                    writer.WriteNumber("y", y);
                    writer.WriteNumber("z", z);
                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.Vector4: {
                    var x = m_Reader.ReadSingle();
                    var y = m_Reader.ReadSingle();
                    var z = m_Reader.ReadSingle();
                    var w = m_Reader.ReadSingle();

                    writer.WriteStartObject();
                    writer.WriteNumber("x", x);
                    writer.WriteNumber("y", y);
                    writer.WriteNumber("z", z);
                    writer.WriteNumber("w", w);
                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.Vector2Int: {
                    var x = m_Reader.ReadInt32();
                    var y = m_Reader.ReadInt32();

                    writer.WriteStartObject();
                    writer.WriteNumber("x", x);
                    writer.WriteNumber("y", y);
                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.AnimationCurve: {
                    var keysCount = m_Reader.ReadInt32();
                    writer.WriteStartObject();

                    writer.WritePropertyName("keys");
                    writer.WriteStartArray();
                    for (var i = 0; i < keysCount; i++) {
                        var time = m_Reader.ReadSingle();
                        var v = m_Reader.ReadSingle();
                        var weightedMode = m_Reader.ReadByte();
                        var inTangent = m_Reader.ReadSingle();
                        var inWeight = m_Reader.ReadSingle();
                        var outTangent = m_Reader.ReadSingle();
                        var outWeight = m_Reader.ReadSingle();

                        writer.WriteStartObject();
                        writer.WriteNumber("time", time);
                        writer.WriteNumber("value", v);
                        writer.WriteNumber("weightedMode", weightedMode);
                        writer.WriteNumber("inTangent", inTangent);
                        writer.WriteNumber("inWeight", inWeight);
                        writer.WriteNumber("outTangent", outTangent);
                        writer.WriteNumber("outWeight", outWeight);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.Gradient: {
                    var colorKeyCount = m_Reader.ReadInt32();
                    writer.WriteStartObject();

                    writer.WritePropertyName("colorKeys");
                    writer.WriteStartArray();
                    for (var i = 0; i < colorKeyCount; i++) {
                        var time = m_Reader.ReadSingle();
                        var r = m_Reader.ReadSingle();
                        var g = m_Reader.ReadSingle();
                        var b = m_Reader.ReadSingle();

                        writer.WriteStartObject();
                        writer.WriteNumber("time", time);
                        writer.WriteNumber("r", r);
                        writer.WriteNumber("g", g);
                        writer.WriteNumber("b", b);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    var alphaKeyCount = m_Reader.ReadInt32();
                    writer.WritePropertyName("alphaKeys");
                    writer.WriteStartArray();
                    for (var i = 0; i < alphaKeyCount; i++) {
                        var time = m_Reader.ReadSingle();
                        var alpha = m_Reader.ReadSingle();

                        writer.WriteStartObject();
                        writer.WriteNumber("time", time);
                        writer.WriteNumber("alpha", alpha);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    var mode = m_Reader.ReadByte();
                    writer.WriteNumber("mode", mode);

                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.ColorBlock: {
                    writer.WriteStartObject();

                    writer.WritePropertyName("normalColor");
                    ReadValueAsJson(writer, ValueSchema.Color());

                    writer.WritePropertyName("pressedColor");
                    ReadValueAsJson(writer, ValueSchema.Color());

                    writer.WritePropertyName("highlightedColor");
                    ReadValueAsJson(writer, ValueSchema.Color());

                    writer.WritePropertyName("disabledColor");
                    ReadValueAsJson(writer, ValueSchema.Color());

                    var colorMultiplier = m_Reader.ReadSingle();
                    var fadeDuration = m_Reader.ReadSingle();

                    writer.WriteNumber("colorMultiplier", colorMultiplier);
                    writer.WriteNumber("fadeDuration", fadeDuration);

                    writer.WriteEndObject();
                    return;
                }

            case ValueKind.Array:
            case ValueKind.List: {
                    var len = m_Reader.ReadInt32();
                    writer.WriteStartArray();
                    for (var i = 0; i < len; i++) {
                        ReadValueAsJson(writer, value.Element!);
                    }
                    writer.WriteEndArray();
                    return;
                }

            case ValueKind.Object: {
                    Guid typeId;
                    TypeSchema? actualSchema;

                    if (value.IsIdentifiedType || value.ForceNeedsType) {
                        typeId = ReadTypeId();
                        if (typeId == Guid.Empty) {
                            writer.WriteNullValue();
                            return;
                        }
                        actualSchema = m_SchemaProvider.Resolve(typeId);
                    } else {
                        typeId = Guid.Empty;
                        actualSchema = value.ObjectType;
                    }
                    /*
                    if (value.IsIdentifiedType) {
                        writer.WriteStartObject();
                        // writer.WriteString("$typeId", typeId.ToString("N"));
                        // writer.WriteString("$typeName", actualSchema?.FullName);

                        writer.WritePropertyName("Data");
                    }
                    */
                    ReadObjectBodyAsJson(writer, actualSchema, value.IsIdentifiedType);
                    /*
                    if (value.IsIdentifiedType) {
                        writer.WriteEndObject();
                    }
                    */
                    return;
                }

            default:
                throw new NotSupportedException("Unsupported kind: " + value.Kind);
        }
    }

    private Guid ReadTypeId() {
        var read = m_Reader.Read(m_TypeIdBuffer, 0, 16);
        if (read != 16) {
            throw new EndOfStreamException("Unexpected EOF while reading TypeId.");
        }

        var g = new Guid(m_TypeIdBuffer);
        return g == Guid.Empty ? Guid.Empty : g;
    }
}
