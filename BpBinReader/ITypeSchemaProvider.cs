namespace BpBinReader;
public interface ITypeSchemaProvider {
    TypeSchema Resolve(Guid typeId);
}
