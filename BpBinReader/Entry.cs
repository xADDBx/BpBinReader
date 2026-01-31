using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BpBinReader;

public class Entry {
    public enum GameType {
        Wrath,
        RogueTrader,
        DarkHeresy
    }
    public static void Main(string[] args) {
        if (args.Length == 1) {

        } else {
            try {
                var installLocation = FindInstallLocation(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "Owlcat Games", "Pathfinder Wrath Of The Righteous", "Player.log"), "Wrath_Data");
                Console.WriteLine($"Found install location: {installLocation}");
                RunForPath(installLocation, GameType.Wrath);
            } catch (NotSupportedException) {
                Console.WriteLine("Could not find Wrath location automatically.");
                return;
            }
            try {
                var installLocation = FindInstallLocation(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "Owlcat Games", "Warhammer 40000 Rogue Trader", "Player.log"), "WH40KRT_Data");
                Console.WriteLine($"Found install location: {installLocation}");
                RunForPath(installLocation, GameType.RogueTrader);
            } catch (NotSupportedException) {
                Console.WriteLine("Could not find Rogue Trader location automatically.");
                return;
            }
            try {
                var installLocation = FindInstallLocation(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "Owlcat Games", "WHDH", "Player.log"), "WH40KDH_Data");
                Console.WriteLine($"Found install location: {installLocation}");
                RunForPath(installLocation, GameType.DarkHeresy);
            } catch (NotSupportedException) {
                Console.WriteLine("Could not find Dark Heresy location automatically.");
                return;
            }
        }
    }
    private static void RunForPath(string basePath, GameType gameType) {
        try {
            var sw = Stopwatch.StartNew();
            ITypeSchemaProvider schemaProvider;
            string outputPath;
            switch (gameType) {
                case GameType.RogueTrader: {
                        schemaProvider = new RogueTraderTypeSchemaProvider([Path.Combine(basePath, "WH40KRT_Data", "Managed")]);
                        outputPath = Path.Combine(AppContext.BaseDirectory, "blueprints_RT.json");
                    }
                    break;
                case GameType.DarkHeresy: {
                        schemaProvider = new DarkHeresyTypeSchemaProvider([Path.Combine(basePath, "WH40KDH_Data", "Managed")]);
                        outputPath = Path.Combine(AppContext.BaseDirectory, "blueprints_DH.json");
                    }
                    break;
                case GameType.Wrath: {
                        schemaProvider = new WrathTypeSchemaProvider([Path.Combine(basePath, "Wrath_Data", "Managed")]);
                        outputPath = Path.Combine(AppContext.BaseDirectory, "blueprints_WotR.json");
                    }
                    break;
                default: throw new NotSupportedException($"Game type {gameType} is not supported.");
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
