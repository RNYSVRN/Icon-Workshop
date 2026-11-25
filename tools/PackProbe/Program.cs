using System.IO;
using System.Diagnostics;
using IconWorkshop.Packs;

if (args.Length == 0) { Console.WriteLine("usage: PackProbe <apk|folder|zip> [outdir]"); return 1; }
IconWorkshop.Loc.Init("de");
string outDir = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "PackProbe-" + Guid.NewGuid().ToString("N"));
var sw = Stopwatch.StartNew();
var store = new PackStore(outDir);
var result = await store.ImportAsync(args[0], new Progress<ImportProgress>(p => { }), CancellationToken.None);
var pack = result.Pack;
Console.WriteLine($"Name:       {pack.Name}");
Console.WriteLine($"Version:    {pack.Version}  Package: {pack.PackageName}");
Console.WriteLine($"Icons:      {pack.Icons.Count}");
Console.WriteLine($"Kategorien: {string.Join(", ", pack.Categories)}");
Console.WriteLine($"Components: {pack.Components.Count}");
Console.WriteLine($"Fehlend: {result.Missing}  Vektor: {result.Unsupported}  Fehler: {result.Failed}");
Console.WriteLine($"Ohne Kategorie: {pack.Icons.Count(i => i.Categories is null)}");
Console.WriteLine($"Größe:      {pack.SizeOnDisk() / 1024 / 1024} MB");
Console.WriteLine($"Zeit:       {sw.Elapsed.TotalSeconds:F1} s");
Console.WriteLine($"Ordner:     {pack.Folder}");
Console.WriteLine($"Beispiele:  {string.Join(", ", pack.Icons.Take(5).Select(i => i.Name))}");
if (args[0].EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
{
    using var zip = System.IO.Compression.ZipFile.OpenRead(args[0]);
    using var s = zip.GetEntry("resources.arsc")!.Open(); using var ms = new MemoryStream(); s.CopyTo(ms);
    var arsc = IconWorkshop.Packs.ArscTable.TryParse(ms.ToArray())!;
    int resolved = pack.Icons.Count(i => arsc.Files("drawable", i.Name).Any(f => zip.GetEntry(f.Path) is not null));
    Console.WriteLine($"ARSC:       {resolved} von {pack.Icons.Count} Icons über resources.arsc auffindbar; steam -> {string.Join(", ", arsc.Files("drawable", "steam").Select(f => f.Path + "@" + f.Density))}");
}
return 0;

