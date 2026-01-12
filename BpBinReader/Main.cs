using HarmonyLib;
using Kingmaker.BundlesLoading;
using UnityModManagerNet;

namespace BpBinReader;

public static class Main {
    internal static Harmony HarmonyInstance = null!;
    internal static UnityModManager.ModEntry.ModLogger Log = null!;

    public static bool Load(UnityModManager.ModEntry modEntry) {
        Log = modEntry.Logger;

        try {
            // TODO: wire a real schema provider (in-game reflection first, MetadataLoadContext later).
            var schemaProvider = new RogueTraderTypeSchemaProvider();

            var packPath = BundlesLoadService.BundlesPath("blueprints-pack.bbp");
            var outputPath = Path.Combine(modEntry.Path, "blueprints.json");

            BinaryToJsonConverter.DumpBlueprintPackToJson(packPath, outputPath, schemaProvider);

            Log.Log($"Wrote {outputPath}");
        }
        catch (Exception ex) {
            Log.Error(ex.ToString());
        }

        return true;
    }
}
