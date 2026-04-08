using AssetsTools.NET;
using AssetsTools.NET.Extra;

public class AssetProvider {
    private readonly List<AssetTypeValueField> m_Entries;
    private readonly Dictionary<long, string> m_SharedKeys = [];

    public (string AssetId, long FileId, long PathId, bool IsString, string? SharedKey) GetEntryAtIndex(int index) {
        var entry = m_Entries[index];
        string assetId = entry["AssetId"].AsString;
        long fileId = entry["FileId"].AsLong;
        long pathId = entry["Asset"]["m_PathID"].AsLong;
        var isString = m_SharedKeys.TryGetValue(pathId, out var sharedKey);
        return (assetId, fileId, pathId, isString, sharedKey);
    }

    public AssetProvider(string path) {
        using var stream = File.OpenRead(path);

        var assetsManager = new AssetsManager();
        var bundleInst = assetsManager.LoadBundleFile(stream);
        AssetTypeValueField? baseField = null;
        List<AssetFileInfo> candidates = [];
        for (int fileIndex = 0; fileIndex < bundleInst.file.GetAllFileNames().Count; fileIndex++) {
            var fileInst = assetsManager.LoadAssetsFileFromBundle(bundleInst, fileIndex);
            foreach (var info in fileInst?.file?.AssetInfos ?? []) {
                try {
                    var asset = info;
                    var field = assetsManager.GetBaseField(fileInst, asset);
                    if (field.TypeName == "MonoBehaviour") {
                        var name = field["m_Name"]?.AsString;
                        if (name == "BlueprintReferencedAssets") {
                            candidates.Add(asset);
                            baseField = field;
                        } else {
                            var stringField = field["String"];
                            if (stringField != null && !stringField.IsDummy) {
                                var keyField = stringField["m_Key"];
                                if (keyField != null && !keyField.IsDummy) {
                                    m_SharedKeys[asset.PathId] = keyField.AsString;
                                }
                            }
                        }
                    }
                } catch (NullReferenceException) { }
            }
        }
        if (candidates.Count != 1) {
            throw new Exception($"Failed to locate a unique BlueprintReferencedAssets asset. Instead found {candidates.Count}");
        }
        m_Entries = baseField!["m_Entries"]["Array"].Children;
        Console.WriteLine($"Read {m_Entries.Count} Assets.");
        Console.WriteLine($"Read {m_SharedKeys.Count} Shared String Assets.");
    }
}