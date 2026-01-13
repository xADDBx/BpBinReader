namespace BpBinReader;

public class Entry {

    public static void Main(string[] args) {
        if (args.Length < 1) {
            Console.WriteLine("Usage: BpBinReader <basePath>");
            Console.WriteLine("  <basePath> The base path to the game installation.");

            return;
        }
        var basePath = args[0];
        // var basePath = "D:\\Games\\Steam\\steamapps\\common\\Warhammer 40,000 Rogue Trader";
        try {
            var schemaProvider = new RogueTraderTypeSchemaProvider([Path.Combine(basePath, "WH40KRT_Data", "Managed")]);

            var packPath = Path.Combine(basePath, "Bundles", "blueprints-pack.bbp");
            var outputPath = Path.Combine(Path.GetDirectoryName(typeof(Entry).Assembly.Location)!, "blueprints.json");

            BinaryToJsonConverter.DumpBlueprintPackToJson(packPath, outputPath, schemaProvider);

            Console.WriteLine($"Wrote {outputPath}");
        }
        catch (Exception ex) {
            Console.WriteLine(ex.ToString());
            throw;
        }
    }
}
