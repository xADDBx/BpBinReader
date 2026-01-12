namespace BpBinReader;
#warning Game Dependent ._.
public enum ValueKind {
    Int32,
    UInt32,
    Int64,
    UInt64,
    Single,
    Double,
    Boolean,
    String,
    EnumInt32,

    BlueprintRef,

    UnityObjectRef,

    Color,
    Color32,
    Vector2,
    Vector3,
    Vector4,
    Vector2Int,
    Gradient,
    AnimationCurve,
    ColorBlock,

    Array,
    List,
    Object
}
