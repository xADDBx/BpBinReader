namespace BpBinReader;
public interface ITypeSchemaProvider {
    TypeSchema Resolve(Guid typeId);
    string GetEnumName(TypeSchema enumType, object value);
    bool UseStringAssetIdType { get; }
    bool SerializedFieldName { get; } 
}
