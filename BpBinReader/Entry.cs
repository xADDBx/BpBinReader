using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BpBinReader;

public class Entry {

    public static void Main(string[] args) {
        if (args.Length < 1) {
            try {
                var installLocation = FindInstallLocation();
                Console.WriteLine($"Found install location: {installLocation}");
                args = [installLocation];
            } catch (NotSupportedException) {
                Console.WriteLine("Usage: BpBinReader <basePath>");
                Console.WriteLine("  <basePath> The base path to the game installation.");
                return;
            }
        }
        var basePath = args[0];
        try {
            var schemaProvider = new RogueTraderTypeSchemaProvider([Path.Combine(basePath, "WH40KRT_Data", "Managed")]);

            var packPath = Path.Combine(basePath, "Bundles", "blueprints-pack.bbp");
            var outputPath = Path.Combine(AppContext.BaseDirectory, "blueprints.json");

            BinaryToJsonConverter.DumpBlueprintPackToJson(packPath, outputPath, schemaProvider);

            Console.WriteLine($"Wrote {outputPath}");
        }
        catch (Exception ex) {
            Console.WriteLine(ex.ToString());
            throw;
        }
    }
    private static string FindInstallLocation() {
        try {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                var rogueTraderDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "Owlcat Games", "Warhammer 40000 Rogue Trader", "Player.log");
                string lineToFind = "Mono path[0]";
                string line = null;
                foreach (var lineIter in File.ReadLines(rogueTraderDataPath)) {
                    if (lineIter.Contains(lineToFind)) {
                        line = lineIter;
                        break;
                    }
                }
                string monoPathRegex = @"^Mono path\[0\] = '(.*?)/WH40KRT_Data/Managed'$";
                Match match = Regex.Match(line, monoPathRegex);
                if (match.Success) {
                    return match.Groups[1].Value;
                }
            }
        } catch { }
        throw new NotSupportedException("Could not automatically find the install location for this OS platform.");
    }
}
