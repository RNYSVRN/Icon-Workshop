using System.Diagnostics;
using System.IO;
using IconWorkshop.Assignments;
using IconWorkshop.Native;
using IconWorkshop.Packs;
using IconWorkshop.Programs;

IconWorkshop.Loc.Init("de");
if (args.Length > 1 && args[0] == "--design")
{
    // All shapes and templates of the icon maker in one PNG (nothing is saved in the app)
    var bitmaps = await Sta.Run(() =>
    {
        var list = new List<System.Windows.Media.Imaging.BitmapSource>();
        var neutral = PackStyle.Neutral();
        foreach (var shape in Enum.GetValues<IconWorkshop.Imaging.TileShape>().Where(s => s != IconWorkshop.Imaging.TileShape.None))
        {
            var d = IconWorkshop.Imaging.MakerDesign.Default();
            d.Shape = shape; d.UsePackPlate = false;
            list.Add(IconWorkshop.Imaging.DesignRenderer.Render(d, neutral));
        }
        foreach (var t in IconWorkshop.Imaging.MakerDesign.Templates())
        {
            var d = IconWorkshop.Imaging.MakerDesign.Default();
            d.Shape = IconWorkshop.Imaging.TileShape.Squircle;
            d.Layers.Add(new IconWorkshop.Imaging.MakerLayer { Kind = IconWorkshop.Imaging.LayerKind.Badge, Text = "3", Size = 0.3, X = 0.3, Y = -0.3 });
            t.Apply(d);
            list.Add(IconWorkshop.Imaging.DesignRenderer.Render(d, neutral));
        }
        foreach (var set in IconWorkshop.Imaging.IconFonts.All)
        {
            var d = IconWorkshop.Imaging.MakerDesign.Default();
            d.Shape = IconWorkshop.Imaging.TileShape.Squircle; d.UsePackPlate = false;
            var g = set.Search(set.Id == "fa-brands" ? "github" : "home").FirstOrDefault() ?? set.Glyphs[0].Glyph;
            d.Layers[0].GlyphSet = set.Id; d.Layers[0].Glyph = g;
            Console.WriteLine($"{set.Id}: {set.Glyphs.Count} glyphs, sample {set.NameOf(g)}");
            list.Add(IconWorkshop.Imaging.DesignRenderer.Render(d, neutral));
        }
        return list;
    });
    int cols = 7, cell = 120;
    var dv2 = new System.Windows.Media.DrawingVisual();
    using (var dc = dv2.RenderOpen())
    {
        dc.DrawRectangle(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 210, 210)), null, new System.Windows.Rect(0, 0, cols * cell, 4 * cell));
        for (int i = 0; i < bitmaps.Count; i++) dc.DrawImage(bitmaps[i], new System.Windows.Rect(i % cols * cell + 6, i / cols * cell + 6, cell - 12, cell - 12));
    }
    var rtb2 = new System.Windows.Media.Imaging.RenderTargetBitmap(cols * cell, 4 * cell, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
    rtb2.Render(dv2);
    var enc2 = new System.Windows.Media.Imaging.PngBitmapEncoder();
    enc2.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb2));
    using (var fs = File.Create(args[1])) enc2.Save(fs);
    return 0;
}
if (args.Length > 1 && args[0] == "--maker")
{
    // Renders sample icons of the icon maker into one PNG (nothing is saved in the app)
    var ps = new PackStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Icon-Workshop", "Packs"));
    ps.Load();
    var samples = await Sta.Run(() =>
    {
        var list = new List<System.Windows.Media.Imaging.BitmapSource>();
        foreach (var p in ps.Packs.Where(p => p.SourceKind == PackSourceKind.Apk).Take(4).Append(null))
        {
            var style = PackStyle.For(p);
            Console.WriteLine($"Stil {(p?.Name ?? "neutral")}: back={style.Back is not null} mask={style.Keep is not null} upon={style.Upon is not null} scale={style.Scale} neutral={style.IsNeutral} gelernt={style.IsLearned} breite={style.ShapeWidth:F2}");
            var steam = IconWorkshop.Native.ShellNative.GetItemImage(@"C:\Games\Steam\Steam.exe", 256);
            byte[] steamPng;
            { var e = new System.Windows.Media.Imaging.PngBitmapEncoder(); e.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(steam!)); using var m = new MemoryStream(); e.Save(m); steamPng = m.ToArray(); }
            var d1 = IconWorkshop.Imaging.MakerDesign.Default();
            d1.Layers[0].Fill = IconWorkshop.Imaging.FillSpec.Of(style.ContrastColor);
            list.Add(IconWorkshop.Imaging.DesignRenderer.Render(d1, style));
            var d2 = new IconWorkshop.Imaging.MakerDesign { UsePackPlate = false, Background = IconWorkshop.Imaging.FillSpec.Of(System.Windows.Media.Color.FromRgb(0x00, 0x1E, 0x36)) };
            d2.Layers.Add(new IconWorkshop.Imaging.MakerLayer { Kind = IconWorkshop.Imaging.LayerKind.Text, Text = "PS", Fill = IconWorkshop.Imaging.FillSpec.Of(System.Windows.Media.Color.FromRgb(0x31, 0xA8, 0xFF)) });
            list.Add(IconWorkshop.Imaging.DesignRenderer.Render(d2, style));
            var d3 = new IconWorkshop.Imaging.MakerDesign();
            var img3 = new IconWorkshop.Imaging.MakerLayer { Kind = IconWorkshop.Imaging.LayerKind.Image, Size = 0.7 }; img3.SetImage(steamPng); d3.Layers.Add(img3);
            list.Add(IconWorkshop.Imaging.DesignRenderer.Render(d3, style));
            var d4 = new IconWorkshop.Imaging.MakerDesign { UsePackPlate = false, Background = new() { Kind = IconWorkshop.Imaging.FillKind.Linear, Color1 = "#FF171A21", Color2 = "#FF2A475E" } };
            var img4 = new IconWorkshop.Imaging.MakerLayer { Kind = IconWorkshop.Imaging.LayerKind.Image, Tint = true, Size = 0.6 }; img4.SetImage(steamPng); d4.Layers.Add(img4);
            list.Add(IconWorkshop.Imaging.DesignRenderer.Render(d4, style));
            var d5 = IconWorkshop.Imaging.MakerDesign.Default();
            IconWorkshop.Imaging.MakerDesign.Templates()[2].Apply(d5);
            d5.Background = new() { Kind = IconWorkshop.Imaging.FillKind.Linear, Color1 = "#FF2563EB", Color2 = "#FF6B2FBF", Angle = 135 };
            d5.Layers[0].Fill = IconWorkshop.Imaging.FillSpec.Of(System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x24));
            d5.Layers[0].Rotation = -30;
            list.Add(IconWorkshop.Imaging.DesignRenderer.Render(d5, style));
        }
        return list;
    });
    var dv = new System.Windows.Media.DrawingVisual();
    using (var dc = dv.RenderOpen())
    {
        dc.DrawRectangle(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)), null, new System.Windows.Rect(0, 0, 5 * 140, 5 * 140));
        for (int i = 0; i < samples.Count; i++) dc.DrawImage(samples[i], new System.Windows.Rect(i % 5 * 140 + 6, i / 5 * 140 + 6, 128, 128));
    }
    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(5 * 140, 5 * 140, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
    rtb.Render(dv);
    var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
    enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
    using (var fs = File.Create(args[1])) enc.Save(fs);
    return 0;
}
if (args.Length > 0 && args[0] == "--suggest")
{
    // Read-only: which icon would the wizard suggest for each program?
    var ps = new PackStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Icon-Workshop", "Packs"));
    ps.Load();
    var matcher = new IconMatcher(args.Length > 1 ? ps.Packs.First(p => p.Name.Contains(args[1], StringComparison.OrdinalIgnoreCase)) : ps.Packs.First());
    var progs = await ProgramScanner.ScanAsync();
    var withShortcut = progs.Where(p => p.Shortcuts.Count > 0).ToList();
    var sugg = withShortcut.Select(matcher.Suggest).ToList();
    foreach (var (p, s) in withShortcut.Zip(sugg))
        Console.WriteLine($"{(s is null ? "  –  " : s.IsConfident ? " ✔ " + s.Score : " ? " + s.Score),-6} {p.Name,-40} {s?.Icon.Name}");
    Console.WriteLine($"Vorschläge: {sugg.Count(s => s is not null)} von {withShortcut.Count}, sicher: {sugg.Count(s => s?.IsConfident == true)}");
    return 0;
}
if (args.Length > 1 && args[0] == "--inspect") { var li = ShellLink.Read(args[1]); Console.WriteLine($"icon: [{li?.IconPath}],{li?.IconIndex} exists={File.Exists(Environment.ExpandEnvironmentVariables(li?.IconPath ?? ""))}"); var img = await Sta.Run(() => IconWorkshop.Native.ShellNative.IconFromLocation(li!.IconPath + "," + li.IconIndex, 48)); Console.WriteLine($"extract: {img?.PixelWidth}"); return 0; }
if (args.Length > 1 && args[0] == "--gen") { _ = System.IO.Packaging.PackUriHelper.UriSchemePack; foreach (var c in IconWorkshop.Imaging.IconGenerator.ContentOptions(new IconWorkshop.Imaging.GenInput { Name = args[1] })) Console.WriteLine($"{c.Kind} {c.GlyphSet} {(c.Value.Length > 0 ? char.ConvertToUtf32(c.Value, 0).ToString("X") : "")} len={c.Value.Length} {IconWorkshop.Imaging.IconFonts.Get(c.GlyphSet).NameOf(c.Value)}"); return 0; }
if (args.Length > 0 && args[0] == "--games") { foreach (var p in (await ProgramScanner.ScanAsync()).Where(p => p.Game is not null || p.IconMissing)) Console.WriteLine($"{p.Name,-30} {p.Game?.Launcher} {p.Game?.Id} missing={p.IconMissing} steamIcon={(p.Game is { Launcher: GameLauncher.Steam } g ? GameLibrary.SteamClientIcon(g.Id) : null)}"); return 0; }
if (args.Length > 0 && args[0] == "--apps") { foreach (var (n, p) in await Sta.Run(() => ShellNative.EnumerateAppsFolder())) Console.WriteLine($"{n} | {p}"); return 0; }
int failures = 0;
void Check(bool ok, string what) { Console.WriteLine((ok ? "OK    " : "FEHLER ") + what); if (!ok) failures++; }

string root = Path.Combine(Path.GetTempPath(), "IW-ShortcutTest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var store = new PackStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Icon-Workshop", "Packs"));
    store.Load();
    var pack = store.Packs.First(p => p.Icons.Any(i => i.Name == "steam") && p.Icons.Any(i => i.Name == "chrome"));
    var icon = pack.Icons.First(i => i.Name == "steam");

    // Test shortcuts (only in the test folder)
    string lnk = Path.Combine(root, "Test Programm ä.lnk");
    await Sta.Run(() => ShellLink.Create(lnk, @"C:\Windows\notepad.exe", null, null, null, @"C:\Windows\notepad.exe"));
    string url = Path.Combine(root, "Test Seite.url");
    File.WriteAllText(url, "[InternetShortcut]\r\nURL=https://example.com/ü\r\n", System.Text.Encoding.Latin1);

    var before = ShellLink.Read(lnk)!;
    Check(before.Target?.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase) == true, $"lnk gelesen: {before.Target}, Icon {before.IconPath},{before.IconIndex}");

    var data = Path.Combine(root, "Data");
    var assignments = new AssignmentStore(data);
    var refs = new[] { lnk, url }.Select(p => new ShortcutRef { Path = p, Location = LinkLocation.Desktop, AllUsers = false, Info = ShellLink.Read(p)! }).ToList();
    var sw = Stopwatch.StartNew();
    var result = await assignments.ApplyAsync(refs, "Test", pack, icon, allowElevation: false);
    Check(result.Succeeded.Count == 2 && result.Failed.Count == 0, $"Zuweisen: {result.Succeeded.Count} ok, {result.Failed.Count} Fehler ({sw.ElapsedMilliseconds} ms)");
    string ico = pack.IcoPath(icon);
    Check(File.Exists(ico) && new FileInfo(ico).Length > 10000, $"ICO erzeugt: {new FileInfo(ico).Length / 1024} KB");
    Check(ShellLink.Read(lnk)!.IconPath == ico, "lnk zeigt auf ICO");
    var urlInfo = ShellLink.Read(url)!;
    Check(urlInfo.IconPath == ico && urlInfo.Url == "https://example.com/ü", $"url zeigt auf ICO, URL erhalten: {urlInfo.Url}");
    Check(assignments.Items.Count == 2 && assignments.Items.All(a => a.BackupFile is not null && File.Exists(a.BackupFile)), "Sicherungen angelegt");
    Check(assignments.UsageCount(pack) == 2, "Nutzung des Packs gezählt");

    // Second assignment keeps the first original
    var icon2 = pack.Icons.First(i => i.Name == "chrome");
    await assignments.ApplyAsync(refs, "Test", pack, icon2, false);
    Check(assignments.Items.First(a => a.Target == lnk).OriginalIconPath == before.IconPath, "Original bleibt nach zweiter Zuweisung erhalten");

    // Reload from disk
    var reloaded = new AssignmentStore(data);
    Check(reloaded.Items.Count == 2, "assignments.json neu geladen");

    var restore = await reloaded.RestoreAsync(reloaded.Items.ToList(), false);
    Check(restore.Failed.Count == 0 && reloaded.Items.Count == 0, "Wiederherstellen");
    var after = ShellLink.Read(lnk)!;
    Check(after.IconPath == before.IconPath && after.IconIndex == before.IconIndex, $"lnk-Icon zurück: {after.IconPath},{after.IconIndex}");
    var urlAfter = ShellLink.Read(url)!;
    Check(urlAfter.IconPath == "" && urlAfter.Url == "https://example.com/ü", "url-Icon entfernt, URL unverändert");

    // Registry icons (test key only, never the real system icon keys)
    {
        const string testBase = @"Software\IconWorkshopTest";
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testBase, false);
        var t1 = new IconWorkshop.Targets.IconTarget { Id = "test:a", Name = "Test A", Subtitle = "", Glyph = "", KeyPath = testBase + @"\A\DefaultIcon", SearchTerms = [] };
        var t2 = new IconWorkshop.Targets.IconTarget { Id = "test:b", Name = "Test B", Subtitle = "", Glyph = "", KeyPath = testBase + @"\B\DefaultIcon", ValueName = "full", SearchTerms = [] };
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(t2.KeyPath)) k.SetValue("full", @"%SystemRoot%\System32\imageres.dll,-54", Microsoft.Win32.RegistryValueKind.ExpandString);
        if (!t1.KeyPath.StartsWith(@"Software\IconWorkshopTest\") || !t2.KeyPath.StartsWith(@"Software\IconWorkshopTest\")) throw new InvalidOperationException("Testschlüssel falsch");
        var reg = new AssignmentStore(Path.Combine(root, "RegData"));
        var r = await reg.ApplyTargetsAsync([t1, t2], pack, icon);
        Check(r.Failed.Count == 0 && t1.CurrentValue == ico + ",0" && t2.CurrentValue == ico + ",0", "Registry-Icons gesetzt");
        var rr = await reg.RestoreAsync(reg.Items.ToList(), false);
        Check(rr.Failed.Count == 0, "Registry wiederhergestellt");
        using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testBase + @"\A")) Check(k is null, "Neu angelegte Schlüssel wieder entfernt");
        using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(t2.KeyPath))
            Check(k?.GetValueKind("full") == Microsoft.Win32.RegistryValueKind.ExpandString && (string?)k.GetValue("full", null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) == @"%SystemRoot%\System32\imageres.dll,-54", "Vorheriger Wert samt Typ zurück");
        var resIcon = await Sta.Run(() => IconWorkshop.Native.ShellNative.IconFromLocation(@"%SystemRoot%\System32\imageres.dll,-54", 48));
        Check(resIcon is not null, $"Icon aus Ressourcen-ID geladen ({resIcon?.PixelWidth} px)");
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testBase, false);
    }

    // Thumbnail photo frame option (test key only, never the real file associations)
    {
        const string tfBase = @"Software\IconWorkshopTest\SFA";
        IconWorkshop.Services.ThumbnailFrame.Root = tfBase;
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\IconWorkshopTest", false);
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(tfBase + @"\.png")) k.SetValue("Keep", "x");
        Check(!IconWorkshop.Services.ThumbnailFrame.IsHidden, "Fotorahmen: Standard erkannt");
        IconWorkshop.Services.ThumbnailFrame.SetHidden(true);
        Check(IconWorkshop.Services.ThumbnailFrame.IsHidden, "Fotorahmen ausgeblendet (Treatment=0 für .png und .ico)");
        IconWorkshop.Services.ThumbnailFrame.SetHidden(false);
        Check(!IconWorkshop.Services.ThumbnailFrame.IsHidden, "Fotorahmen wieder Windows-Standard");
        using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(tfBase + @"\.ico")) Check(k is null, "Fotorahmen: angelegter Schlüssel entfernt");
        using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(tfBase + @"\.png")) Check((string?)k?.GetValue("Keep") == "x" && k.GetValue("Treatment") is null, "Fotorahmen: vorhandener Schlüssel samt anderer Werte bleibt");
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\IconWorkshopTest", false);
    }

    // Explorer context menu (test key only, never the real file classes)
    {
        const string emBase = @"Software\IconWorkshopTest\Classes";
        IconWorkshop.Services.ExplorerMenu.Root = emBase;
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\IconWorkshopTest", false);
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(emBase + @"\Directory\shell\Other")) k.SetValue("", "keep");
        Check(!IconWorkshop.Services.ExplorerMenu.IsEnabled, "Kontextmenü: anfangs aus");
        IconWorkshop.Services.ExplorerMenu.SetEnabled(true, @"C:\Apps\IconWorkshop.exe");
        string? cmdText;
        using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(emBase + @"\lnkfile\shell\IconWorkshop\command")) cmdText = k?.GetValue("") as string;
        Check(IconWorkshop.Services.ExplorerMenu.IsEnabled && cmdText == "\"C:\\Apps\\IconWorkshop.exe\" --open \"%1\"", $"Kontextmenü an für Verknüpfungen, Ordner, Laufwerke ({cmdText})");
        IconWorkshop.Services.ExplorerMenu.RefreshPath(@"D:\Neu\IconWorkshop.exe");
        using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(emBase + @"\Drive\shell\IconWorkshop\command")) cmdText = k?.GetValue("") as string;
        Check(cmdText?.Contains(@"D:\Neu\IconWorkshop.exe") == true, "Kontextmenü folgt einem verschobenen Programm");
        IconWorkshop.Services.ExplorerMenu.SetEnabled(false, "");
        using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(emBase + @"\Directory\shell"))
            Check(!IconWorkshop.Services.ExplorerMenu.IsEnabled && k?.OpenSubKey("IconWorkshop") is null && k?.OpenSubKey("Other") is not null, "Kontextmenü aus, fremde Einträge bleiben");
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\IconWorkshopTest", false);
    }

    // Explorer passes drives as "C:\" -> argument C:" ; plural rule only for real endings
    {
        Check(IconWorkshop.App.CleanOpenPath("C:\"") == @"C:\" && IconWorkshop.App.CleanOpenPath(@"D:\Daten") == @"D:\Daten" && IconWorkshop.App.CleanOpenPath("\"E:\\x.lnk\"") == @"E:\x.lnk",
              "Kontextmenü-Pfad: Laufwerk mit verschlucktem Anführungszeichen repariert");
        Check(IconWorkshop.Imaging.GlyphNames.Translate("projekte") == "work" && IconWorkshop.Imaging.GlyphNames.Translate("autodesk") is null && IconWorkshop.Imaging.GlyphNames.Translate("fotos") == "photo",
              "Übersetzung: Mehrzahl ja, Markennamen nicht (Autodesk)");
    }

    // Games: Steam/Epic shortcuts, missing icon files, Steam's small game icon
    {
        LinkInfo L(string? target = null, string? args = null, string? url = null, string icon = "") => new("x", target, args, null, null, url, icon, 0);
        var g1 = GameLibrary.Detect(L(url: "steam://rungameid/431960"));
        var g2 = GameLibrary.Detect(L(@"C:\Games\Steam\steam.exe", "-applaunch 570 -novid"));
        var g3 = GameLibrary.Detect(L(url: "com.epicgames.launcher://apps/fn%3A4fe75bbc%3AFortnite?action=launch&silent=true"));
        Check(g1 == new GameInfo(GameLauncher.Steam, "431960") && g2 == new GameInfo(GameLauncher.Steam, "570") && g3 == new GameInfo(GameLauncher.Epic, "Fortnite")
              && GameLibrary.Detect(L(@"C:\Windows\notepad.exe")) is null, "Spiele erkannt: Steam (Internet-Verknüpfung, -applaunch), Epic; Notepad nicht");
        Check(GameLibrary.IconFileMissing(L(url: "steam://rungameid/1", icon: Path.Combine(root, "fehlt.ico"))) && !GameLibrary.IconFileMissing(L(@"C:\Windows\notepad.exe", icon: @"C:\Windows\notepad.exe"))
              && !GameLibrary.IconFileMissing(L(@"C:\Windows\notepad.exe")), "Fehlende Icon-Datei erkannt");
        string steam = Path.Combine(root, "Steam");
        string cache = Path.Combine(steam, "appcache", "librarycache", "123");
        Directory.CreateDirectory(cache);
        File.WriteAllBytes(Path.Combine(cache, "header.jpg"), new byte[20000]);
        File.WriteAllBytes(Path.Combine(cache, new string('a', 40) + ".jpg"), new byte[900]);
        Check(GameLibrary.SteamClientIcon("123", steam)?.EndsWith(new string('a', 40) + ".jpg") == true && GameLibrary.SteamClientIcon("999", steam) is null, "Steams kleines Spiel-Icon gefunden");
    }

    // Profiles: files in a test folder
    {
        IconWorkshop.Services.Profiles.Folder = Path.Combine(root, "Profiles");
        string f1 = IconWorkshop.Services.Profiles.FileFor("Dunkel/Nacht");
        File.WriteAllText(f1, "x");
        File.WriteAllText(IconWorkshop.Services.Profiles.FileFor("Bunt"), "x");
        var all = IconWorkshop.Services.Profiles.All();
        Check(Path.GetFileName(f1) == "Dunkel_Nacht.zip" && all.Select(p => p.Name).SequenceEqual(["Bunt", "Dunkel_Nacht"]), "Profile: sichere Dateinamen, sortierte Liste");
        IconWorkshop.Services.Profiles.Delete(all[0]);
        Check(IconWorkshop.Services.Profiles.All().Count == 1, "Profil gelöscht");
    }

    // Folder icons (test folders only)
    {
        var store3 = new AssignmentStore(Path.Combine(root, "FolderData"));
        string plain = Path.Combine(root, "Ordner ohne ini");
        string withIni = Path.Combine(root, "Ordner mit ini ä");
        Directory.CreateDirectory(plain);
        Directory.CreateDirectory(withIni);
        string iniPath = Path.Combine(withIni, "desktop.ini");
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nLocalizedResourceName=@%SystemRoot%\\system32\\shell32.dll,-21770\r\nIconResource=%SystemRoot%\\system32\\imageres.dll,-112\r\n", System.Text.Encoding.Unicode);
        File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);
        byte[] originalIni = File.ReadAllBytes(iniPath);
        var plainAttrs = File.GetAttributes(plain);

        var targets = new[] { plain, withIni }.Select(f => new IconWorkshop.Targets.IconTarget
            { Id = IconWorkshop.Targets.FolderIcons.IdOf(f), Name = Path.GetFileName(f), Subtitle = "", Glyph = "", FolderPath = f, SearchTerms = [] }).ToList();
        var fr = await store3.ApplyTargetsAsync(targets, pack, icon);
        Check(fr.Failed.Count == 0, "Ordner-Icons gesetzt" + string.Concat(fr.Failed.Select(f => " " + f.Error)));
        string plainIni = File.ReadAllText(Path.Combine(plain, "desktop.ini"));
        Check(plainIni.Contains("IconResource=" + ico + ",0") && (File.GetAttributes(Path.Combine(plain, "desktop.ini")) & FileAttributes.Hidden) != 0
              && (File.GetAttributes(plain) & FileAttributes.ReadOnly) != 0, "desktop.ini angelegt, versteckt, Ordner markiert");
        string withText = File.ReadAllText(iniPath);
        Check(withText.Contains("LocalizedResourceName=@%SystemRoot%\\system32\\shell32.dll,-21770") && withText.Contains("IconResource=" + ico + ",0")
              && !withText.Contains("imageres.dll,-112"), "Vorhandene desktop.ini ergänzt, Name erhalten");
        var frr = await store3.RestoreAsync(store3.Items.ToList(), false);
        Check(frr.Failed.Count == 0 && !File.Exists(Path.Combine(plain, "desktop.ini")) && File.GetAttributes(plain) == plainAttrs, "Ordner ohne ini: zurückgesetzt");
        Check(File.ReadAllBytes(iniPath).SequenceEqual(originalIni) && (File.GetAttributes(iniPath) & (FileAttributes.Hidden | FileAttributes.System)) == (FileAttributes.Hidden | FileAttributes.System),
              "Ordner mit ini: Original byte-genau zurück");
    }

    // File types (made-up test extension only)
    {
        const string ext = ".iwtestfile";
        const string progId = "IconWorkshop.TestFile";
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ext)) k.SetValue("", progId);
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progId)) k.SetValue("", "Icon Workshop Testdatei");
        try
        {
            var t = IconWorkshop.Targets.FileTypeIcons.For(ext);
            if (t.KeyPath != @"Software\Classes\" + progId + @"\DefaultIcon") throw new InvalidOperationException("Testschlüssel falsch: " + t.KeyPath);
            Check(t.Subtitle == "Icon Workshop Testdatei", $"Dateityp erkannt: {t.Name}, {t.Subtitle}");
            var store4 = new AssignmentStore(Path.Combine(root, "TypeData"));
            var tr = await store4.ApplyTargetsAsync([t], pack, icon);
            Check(tr.Failed.Count == 0 && t.CurrentValue == ico + ",0", "Dateityp-Icon gesetzt");
            var trr = await store4.RestoreAsync(store4.Items.ToList(), false);
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + progId + @"\DefaultIcon")) Check(trr.Failed.Count == 0 && k is null, "Dateityp zurückgesetzt");
            var ico2 = await Sta.Run(() => IconWorkshop.Native.ShellNative.IconForExtension(".txt"));
            Check(ico2 is not null, $"Icon für .txt aus der Systemliste ({ico2?.PixelWidth} px)");
            var unsupported = IconWorkshop.Targets.FileTypeIcons.For(".ico");
            Console.WriteLine($"      .ico: {(unsupported.Unsupported is null ? "änderbar" : "eigenes Icon, gesperrt")}");
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ext, false);
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + progId, false);
        }
    }

    // Status, re-apply, export and import (test shortcut only)
    {
        string lnk5 = Path.Combine(root, "Update Test.lnk");
        await Sta.Run(() => ShellLink.Create(lnk5, @"C:\Windows\notepad.exe", null, null, null, @"C:\Windows\notepad.exe"));
        var storeA = new AssignmentStore(Path.Combine(root, "DataA"));
        var ref5 = new ShortcutRef { Path = lnk5, Location = LinkLocation.Desktop, AllUsers = false, Info = ShellLink.Read(lnk5)! };
        await storeA.ApplyAsync([ref5], "Update Test", pack, icon, false);
        var a5 = storeA.Find(lnk5)!;
        Check(storeA.GetStatus(a5) == AssignmentStatus.Ok, "Status: in Ordnung");
        await Sta.Run(() => ShellLink.SetIcon(lnk5, @"C:\Windows\notepad.exe", 0)); // like a program update
        Check(storeA.GetStatus(a5) == AssignmentStatus.Changed, "Status: überschrieben erkannt");
        var re = await storeA.ReapplyAsync(store, false);
        Check(re.Repaired == 1 && storeA.GetStatus(a5) == AssignmentStatus.Ok, $"Erneut angewendet ({re.Repaired})");

        string zip = Path.Combine(root, "export.zip");
        int exported = await storeA.ExportAsync(zip);
        Check(exported == 1 && File.Exists(zip), "Export geschrieben");
        await storeA.RestoreAsync(storeA.Items.ToList(), false);
        Check(ShellLink.Read(lnk5)!.IconPath.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase), "Vor dem Import zurückgesetzt");
        var storeB = new AssignmentStore(Path.Combine(root, "DataB"));
        var imp = await storeB.ImportAsync(zip, store);
        Check(imp.Failed.Count == 0 && storeB.Items.Count == 1 && storeB.GetStatus(storeB.Items[0]) == AssignmentStatus.Ok, "Import angewendet");
        await storeB.RestoreAsync(storeB.Items.ToList(), false);
        File.Delete(lnk5);
        Check(storeB.Items.Count == 0, "Nach Import wiederhergestellt");
    }

    // Many programs at once (test shortcuts only)
    {
        string l1 = Path.Combine(root, "Viele 1.lnk"), l2 = Path.Combine(root, "Viele 2.lnk");
        foreach (var l in new[] { l1, l2 }) await Sta.Run(() => ShellLink.Create(l, @"C:\Windows\notepad.exe", null, null, null, @"C:\Windows\notepad.exe"));
        var storeM = new AssignmentStore(Path.Combine(root, "DataM"));
        var chrome = pack.Icons.First(i => i.Name == "chrome");
        var items = new List<AssignmentStore.BatchItem>
        {
            new([new ShortcutRef { Path = l1, Location = LinkLocation.Desktop, AllUsers = false, Info = ShellLink.Read(l1)! }], "Viele 1", pack, icon),
            new([new ShortcutRef { Path = l2, Location = LinkLocation.Desktop, AllUsers = false, Info = ShellLink.Read(l2)! }], "Viele 2", pack, chrome),
        };
        var many = await storeM.ApplyManyAsync(items, null);
        Check(many.Failed.Count == 0 && many.Succeeded.Count == 2 && ShellLink.Read(l2)!.IconPath == pack.IcoPath(chrome), "Mehrere Programme in einem Schritt");
        await storeM.RestoreAsync(storeM.Items.ToList(), false);
        Check(storeM.Items.Count == 0 && ShellLink.Read(l1)!.IconPath.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase), "Mehrere wiederhergestellt");
        var m = new IconMatcher(pack);
        var fake = new ProgramEntry { Key = "x", Name = "Google Chrome", Kind = ProgramKind.Shortcuts, ExePath = @"C:\x\chrome.exe" };
        var s1 = m.Suggest(fake);
        Check(s1?.Icon.Name == "chrome" && s1.IsConfident, $"Vorschlag Google Chrome -> {s1?.Icon.Name} ({s1?.Score})");
        var fake2 = new ProgramEntry { Key = "y", Name = "Task Manager", Kind = ProgramKind.Shortcuts, ExePath = @"C:\Windows\System32\Taskmgr.exe" };
        Check(m.Suggest(fake2) is null || m.Suggest(fake2)!.Icon.Name.Contains("task", StringComparison.OrdinalIgnoreCase), $"Kein Zufallstreffer für Task Manager ({m.Suggest(fake2)?.Icon.Name ?? "keiner"})");
    }

    // One-click generator: contents, variants, locks, colors
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// (embedded fonts) without a WPF Application
        var steam = IconWorkshop.Imaging.IconGenerator.ContentOptions(new IconWorkshop.Imaging.GenInput { Name = "Steam" });
        Check(steam.Count > 0 && steam[0].GlyphSet == "fa-brands", $"Generator: Markenlogo zuerst für Steam ({steam.FirstOrDefault()?.GlyphSet})");
        var music = IconWorkshop.Imaging.IconGenerator.ContentOptions(new IconWorkshop.Imaging.GenInput { Name = "Musik" });
        var musicNames = music.Where(c => c.Kind == IconWorkshop.Imaging.GenContentKind.Glyph).Select(c => IconWorkshop.Imaging.IconFonts.Get(c.GlyphSet).NameOf(c.Value) ?? "").ToList();
        Check(music.Where(c => c.Kind == IconWorkshop.Imaging.GenContentKind.Glyph).All(c => IconWorkshop.Imaging.IconFonts.Get(c.GlyphSet).Has(c.Value)), "Generator: nur Symbole, die in der Schrift vorhanden sind");
        int missing = IconWorkshop.Imaging.IconFonts.All.Sum(s => s.Glyphs.Count(g => !s.Has(g.Glyph)));
        Console.WriteLine($"INFO  Symbole ohne Zeichen in der Schrift: {missing} ({string.Join(", ", IconWorkshop.Imaging.IconFonts.All.Select(s => s.Id + "=" + s.Glyphs.Count(g => !s.Has(g.Glyph))))})");
        Check(musicNames.Count >= 3 && musicNames.All(n => !n.Contains("off")), $"Generator: deutsche Suche, keine verneinenden Symbole ({string.Join(", ", musicNames.Take(4))})");
        Check(IconWorkshop.Imaging.IconGenerator.Monogram("Visual Studio Code") == "VC" && IconWorkshop.Imaging.IconGenerator.Monogram("Steam") == "St",
              $"Generator: Initialen ({IconWorkshop.Imaging.IconGenerator.Monogram("Visual Studio Code")}, {IconWorkshop.Imaging.IconGenerator.Monogram("Steam")})");
        var input = new IconWorkshop.Imaging.GenInput { Name = "Projekte" };
        var a = IconWorkshop.Imaging.IconGenerator.Roll(input, 18, 0);
        var b = IconWorkshop.Imaging.IconGenerator.Roll(input, 18, 0);
        Check(a.Count == 18 && a.Distinct().Count() == 18 && a.SequenceEqual(b), "Generator: 18 verschiedene Varianten, gleich bei gleichem Startwert");
        var keep = a[3];
        var locked = IconWorkshop.Imaging.IconGenerator.Roll(input, 18, 12345, new IconWorkshop.Imaging.GenLocks(false, true, true, false), keep);
        Check(locked.Count > 1 && locked.All(p => p.Palette == keep.Palette && p.Shape == keep.Shape), "Generator: festgehaltene Farben und Form bleiben beim Würfeln");
        // Main color of a red program icon, and a full render of a variant with it
        var redPng = await Sta.Run(() =>
        {
            var dv = new System.Windows.Media.DrawingVisual();
            using (var dc = dv.RenderOpen()) dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD0, 0x20, 0x20)), null, new System.Windows.Point(32, 32), 28, 28);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(64, 64, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        });
        var main = IconWorkshop.Imaging.IconGenerator.MainColor(redPng);
        Check(main is { } mc && mc.R > 150 && mc.G < 80 && mc.B < 80, $"Generator: Hauptfarbe eines roten Icons ({main})");
        var withIcon = new IconWorkshop.Imaging.GenInput { Name = "Rot", ProgramPng = redPng, Style = PackStyle.For(pack), StylePackId = pack.Id };
        var gap = IconWorkshop.Imaging.IconGenerator.InPackStyle(withIcon);
        var gapPng = await Sta.Run(() => IconWorkshop.Imaging.DesignRenderer.RenderPng(gap, withIcon.Style));
        Check(gap.Layers.Count == 1 && gap.Layers[0].Kind == IconWorkshop.Imaging.LayerKind.Image && gapPng.Length > 1000,
              $"Generator: Programm-Icon im Pack-Stil ({gap.Shape}, {gapPng.Length} Bytes)");
        var options = IconWorkshop.Imaging.IconGenerator.ContentOptions(withIcon);
        Check(options.Any(o => o.Kind == IconWorkshop.Imaging.GenContentKind.ProgramIcon), "Generator: Programm-Icon ist eine Variante");

        // Stage 3: Windows shapes, categories, palettes, series
        var cats = IconWorkshop.Imaging.IconCategories.All;
        var folderIn = new IconWorkshop.Imaging.GenInput { Name = "Projekte", Category = IconWorkshop.Imaging.IconCategories.Folder };
        var folderOpts = IconWorkshop.Imaging.IconGenerator.ContentOptions(folderIn);
        var folderRoll = IconWorkshop.Imaging.IconGenerator.Roll(folderIn, 12, 0);
        Check(folderOpts[0].Kind == IconWorkshop.Imaging.GenContentKind.None && folderRoll.All(p => p.Shape == IconWorkshop.Imaging.TileShape.Folder),
              "Kategorie Ordner: leerer Ordner zuerst, nur Ordnerform");
        var typeOpts = IconWorkshop.Imaging.IconGenerator.ContentOptions(new IconWorkshop.Imaging.GenInput { Name = ".psd", Category = IconWorkshop.Imaging.IconCategories.FileType });
        var driveOpts = IconWorkshop.Imaging.IconGenerator.ContentOptions(new IconWorkshop.Imaging.GenInput { Name = "D: Daten (D:)", Category = IconWorkshop.Imaging.IconCategories.Drive });
        Check(typeOpts[0].Value == "PSD" && driveOpts[0].Value == "D:", $"Kategorie Dateityp/Laufwerk: Endung und Buchstabe zuerst ({typeOpts[0].Value}, {driveOpts[0].Value})");
        // Shapes render with transparent corners outside the silhouette and color inside
        foreach (var shape in new[] { IconWorkshop.Imaging.TileShape.Folder, IconWorkshop.Imaging.TileShape.Document, IconWorkshop.Imaging.TileShape.Drive })
        {
            var d = new IconWorkshop.Imaging.MakerDesign { Shape = shape, UsePackPlate = false };
            var px = await Sta.Run(() =>
            {
                var bmp = IconWorkshop.Imaging.DesignRenderer.Render(d, PackStyle.Neutral());
                var buf = new byte[256 * 256 * 4];
                bmp.CopyPixels(buf, 256 * 4, 0);
                return (Corner: buf[(20 * 256 + 236) * 4 + 3], Center: buf[(150 * 256 + 128) * 4 + 3]);
            });
            Check(px.Corner == 0 && px.Center == 255, $"Form {shape}: Ecke frei, Mitte gefüllt ({px.Corner}/{px.Center})");
        }
        // Pastel background: dark content color, so it stays readable
        var pastel = IconWorkshop.Imaging.PaletteFamilies.Pastel.Palettes[3];
        var pd = IconWorkshop.Imaging.IconGenerator.Compose(new IconWorkshop.Imaging.GenParts(new IconWorkshop.Imaging.GenContent(IconWorkshop.Imaging.GenContentKind.Monogram, "Ab"), pastel, IconWorkshop.Imaging.TileShape.Squircle, IconWorkshop.Imaging.GenLook.Flat), new IconWorkshop.Imaging.GenInput { Name = "Ab" });
        var ink = pd.Layers[0].Fill.C1;
        Check(ink.R < 80 && ink.G < 80, $"Pastell: dunkle Schrift ({ink})");
        // Series: same shape/look/colors, content follows the name; folder layer sits on the front panel
        var templ = folderRoll.First(p => p.Content.Kind == IconWorkshop.Imaging.GenContentKind.None) with
        { Content = new IconWorkshop.Imaging.GenContent(IconWorkshop.Imaging.GenContentKind.Glyph, "x", "material") };
        var s1 = IconWorkshop.Imaging.IconGenerator.ForName(templ, new IconWorkshop.Imaging.GenInput { Name = "Musik", Category = IconWorkshop.Imaging.IconCategories.Folder });
        var s2 = IconWorkshop.Imaging.IconGenerator.ForName(templ, new IconWorkshop.Imaging.GenInput { Name = "Fotos", Category = IconWorkshop.Imaging.IconCategories.Folder });
        var sd = IconWorkshop.Imaging.IconGenerator.Compose(s1, new IconWorkshop.Imaging.GenInput { Name = "Musik", Category = IconWorkshop.Imaging.IconCategories.Folder });
        Check(s1.Shape == templ.Shape && s1.Palette == templ.Palette && s1.Look == templ.Look && s1.Content.Kind == IconWorkshop.Imaging.GenContentKind.Glyph
              && s1.Content.Value != s2.Content.Value && sd.Layers[0].Y > 0.05 && sd.Layers[0].Size <= 0.42,
              $"Serie: gleicher Stil, Symbol je Name, Inhalt auf der Ordner-Vorderseite ({cats.Count} Kategorien)");
    }

    // Icon maker: render and save into a test packs folder
    {
        var testStore = new PackStore(Path.Combine(root, "MakerPacks"));
        testStore.Load();
        var design = IconWorkshop.Imaging.MakerDesign.Default();
        design.Layers.Add(new IconWorkshop.Imaging.MakerLayer { Kind = IconWorkshop.Imaging.LayerKind.Badge, Text = "2", Size = 0.3, X = 0.3, Y = -0.3 });
        var png = await Sta.Run(() => IconWorkshop.Imaging.DesignRenderer.RenderPng(design, PackStyle.For(pack)));
        var (cp, ci) = await Sta.Run(() => testStore.SaveCustomIcon("Mein Spiel", png, pack.Name, design.ToJson()));
        var (_, ci2) = await Sta.Run(() => testStore.SaveCustomIcon("Mein Spiel", png, pack.Name));
        Check(cp.Icons.Count == 2 && ci.Name == "mein_spiel" && ci2.Name == "mein_spiel_2" && File.Exists(cp.PngPath(ci)), $"Eigene Icons gespeichert ({ci.Name}, {ci2.Name})");
        Check(cp.Thumbs?.Count == 2 && IconWorkshop.Packs.IconPack.Load(cp.Folder).Icons.Count == 2, "Vorschau-Cache und pack.json aktuell");
        var designFile = PackStore.DesignPath(cp, ci);
        var reloadedDesign = designFile is null ? null : IconWorkshop.Imaging.MakerDesign.FromJson(File.ReadAllText(designFile));
        Check(reloadedDesign is not null && reloadedDesign.Layers.Count == 2 && reloadedDesign.Layers[1].Text == "2" && PackStore.DesignPath(cp, ci2) is null, "Design gespeichert und wieder lesbar");
        var png2 = await Sta.Run(() => { design.Layers[1].Text = "9"; return IconWorkshop.Imaging.DesignRenderer.RenderPng(design, PackStyle.For(pack)); });
        await Sta.Run(() => testStore.UpdateCustomIcon(cp, ci, png2, design.ToJson()));
        var updated = IconWorkshop.Imaging.MakerDesign.FromJson(File.ReadAllText(PackStore.DesignPath(cp, ci)!));
        Check(File.ReadAllBytes(cp.PngPath(ci)).SequenceEqual(png2) && updated?.Layers[1].Text == "9", "Eigenes Icon überschrieben (Bearbeiten)");
        testStore.Load();
        Check(testStore.Find(PackStore.CustomPackId) is not null, "Pack „Eigene Icons“ wird wieder geladen");
        foreach (var p in testStore.Packs) p.Thumbs?.Dispose();
    }

    // Learned shape: a folder pack of 60 portrait icons (no style images) gets the portrait shape
    {
        string src = Path.Combine(root, "Hochformat-Icons");
        Directory.CreateDirectory(src);
        foreach (var pi in pack.Icons.Take(60)) File.Copy(pack.PngPath(pi), Path.Combine(src, pi.Name + ".png"));
        var learnStore = new PackStore(Path.Combine(root, "LearnPacks"));
        var learned = (await learnStore.ImportAsync(src, null, CancellationToken.None)).Pack;
        var style = await Sta.Run(() => PackStyle.For(learned));
        Check(style.IsLearned && style.ShapeWidth is > 0.5 and < 0.8, $"Form aus den Icons abgeleitet (gelernt={style.IsLearned}, Breite {style.ShapeWidth:F2})");
        foreach (var p in learnStore.Packs) p.Thumbs?.Dispose();
    }

    // Program scan (read-only)
    sw.Restart();
    var programs = await ProgramScanner.ScanAsync();
    Console.WriteLine($"Scan: {programs.Count} Programme in {sw.ElapsedMilliseconds} ms; " +
        $"mit Verknüpfung {programs.Count(p => p.Shortcuts.Count > 0)}, mehrere Verknüpfungen {programs.Count(p => p.Shortcuts.Count > 1)}, " +
        $"Windows-Apps {programs.Count(p => p.Kind == ProgramKind.StoreApp)}, nur installiert {programs.Count(p => p.Kind == ProgramKind.InstalledOnly)}, " +
        $"alle Benutzer {programs.Count(p => p.Shortcuts.Any(s => s.AllUsers))}");
    foreach (var p in programs.Where(p => p.Shortcuts.Count > 1).Take(5)) Console.WriteLine($"  {p.Name}: {p.Subtitle}");
    foreach (var p in programs.Where(p => p.Kind == ProgramKind.InstalledOnly).Take(12)) Console.WriteLine($"  Tool: {p.Name} [{p.ExePath}]");
    foreach (var p in programs.Where(p => p.Kind == ProgramKind.StoreApp).Take(3)) Console.WriteLine($"  App: {p.Name} [{p.Aumid}] {p.Subtitle}");
    var img = await Sta.Run(() => ShellNative.GetItemImage(programs.First(p => p.Shortcuts.Count > 0).IconSource, 48));
    Check(img is not null && img.PixelWidth >= 48, $"Shell-Icon geladen ({img?.PixelWidth} px)");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
Console.WriteLine(failures == 0 ? "ALLE TESTS OK" : $"{failures} FEHLER");
return failures;
