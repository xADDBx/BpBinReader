using AssetsTools.NET;
using AssetsTools.NET.Extra;

public class AssetProvider {
    private readonly List<AssetTypeValueField> m_Entries;
    public (string AssetId, long FileId) GetEntryAtIndex(int index) {
        var entry = m_Entries[index];
        string assetId = entry["AssetId"].AsString;
        long fileId = entry["FileId"].AsLong;
        return (assetId, fileId);
    }
    public AssetProvider(string path) {
        using var stream = File.OpenRead(path);

        var assetsManager = new AssetsManager();
        var bundleInst = assetsManager.LoadBundleFile(stream);
        AssetTypeValueField? baseField = null;
        List<AssetFileInfo> candidates = [];
        for (int fileIndex = 0; fileIndex < bundleInst.file.GetAllFileNames().Count; fileIndex++) {
            var fileInst = assetsManager.LoadAssetsFileFromBundle(bundleInst, 0);
            foreach (var info in fileInst?.file?.AssetInfos ?? []) {
                try {
                    var asset = info;
                    baseField = assetsManager.GetBaseField(fileInst, asset);
                    if (baseField.TypeName == "MonoBehaviour" && baseField["m_Name"]?.AsString is string scriptType && scriptType == "BlueprintReferencedAssets") {
                        // For some reason; at least in Wrath there are two of those; both of which have the same amount of entries. So I'll just skip here
                        candidates.Add(asset);
                        goto Found;
                    }
                } catch (NullReferenceException) { }
            }
        }
    Found:
        if (candidates.Count != 1) {
            throw new Exception($"Failed to locate a unique BlueprintReferencedAssets asset. Instead found {candidates.Count}");
        }
        m_Entries = baseField!["m_Entries"]["Array"].Children;
        Console.WriteLine($"Read {m_Entries.Count} Assets.");
    }
}