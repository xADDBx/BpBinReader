using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BpBinReader;

public class Entry {

    public static void Main(string[] args) {
        if (args.Length == 1) {

        } else {
            try {
                var installLocation = FindInstallLocation(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "Owlcat Games", "Warhammer 40000 Rogue Trader", "Player.log"), "WH40KRT_Data");
                Console.WriteLine($"Found install location: {installLocation}");
                RunForPath(installLocation);
            } catch (NotSupportedException) {
                Console.WriteLine("Usage: BpBinReader <basePath>");
                Console.WriteLine("Rogue Trader location.");
                return;
            }
            try {
                var installLocation = FindInstallLocation(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "Owlcat Games", "WHDH", "Player.log"), "WH40KDH_Data");
                Console.WriteLine($"Found install location: {installLocation}");
                RunForPath(installLocation);
            } catch (NotSupportedException) {
                Console.WriteLine("Could not find Dark Heresy location automatically.");
                return;
            }
        }
    }
    private static void RunForPath(string basePath) {
        try {
            var sw = Stopwatch.StartNew();
            ITypeSchemaProvider schemaProvider;
            string outputPath;
            if (basePath.Contains("Rogue Trader")) {
                schemaProvider = new RogueTraderTypeSchemaProvider([Path.Combine(basePath, "WH40KRT_Data", "Managed")]);
                outputPath = Path.Combine(AppContext.BaseDirectory, "blueprints_RT.json");
            } else {
                schemaProvider = new DarkHeresyTypeSchemaProvider([Path.Combine(basePath, "WH40KDH_Data", "Managed")]);
                outputPath = Path.Combine(AppContext.BaseDirectory, "blueprints_DH.json");
            }
            var packPath = Path.Combine(basePath, "Bundles", "blueprints-pack.bbp");

            BinaryToJsonConverter.DumpBlueprintPackToJson(packPath, outputPath, schemaProvider);

            Console.WriteLine($"Wrote {outputPath} in {sw.ElapsedMilliseconds}ms");
        } catch (Exception ex) {
            Console.WriteLine(ex.ToString());
            throw;
        }
    }
    private static string FindInstallLocation(string path, string dataName) {
        try {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                string lineToFind = "Mono path[0]";
                string? line = null;
                foreach (var lineIter in File.ReadLines(path)) {
                    if (lineIter.Contains(lineToFind)) {
                        line = lineIter;
                        break;
                    }
                }
                string monoPathRegex = $@"^Mono path\[0\] = '(.*?)/{dataName}/Managed'$";
                Match match = Regex.Match(line!, monoPathRegex);
                if (match.Success) {
                    return match.Groups[1].Value;
                }
            }
        } catch { }
        throw new NotSupportedException("Could not automatically find the install location for this OS platform.");
    }
}
