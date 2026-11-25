using System.Globalization;

namespace IconWorkshop;

/// <summary>
/// Bilingual texts (German/English). Defaults to the Windows display language: German for "de-*",
/// otherwise English. Can be overridden via "Language" in settings.json or --lang de|en.
/// </summary>
public static class Loc
{
    public static bool German { get; private set; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

    public static void Init(string? language)
    {
        German = language?.ToLowerInvariant() switch
        {
            "de" => true,
            "en" => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de",
        };
    }

    public static string T(string de, string en) => German ? de : en;

    public static string AppName => T("Icon Werkstatt", "Icon Workshop");
    public static string Tagline => T("Icon-Packs für Windows", "Icon packs for Windows");
    public const string Author = "Ronny Severin";

    // ---------- Navigation ----------
    public static string NavPrograms => T("Programme", "Programs");
    public static string NavSystemIcons => T("Systemsymbole", "System icons");
    public static string NavDrives => T("Laufwerke", "Drives");
    public static string NavFolders => T("Ordner", "Folders");
    public static string NavFileTypes => T("Dateitypen", "File types");
    public static string NavLibrary => T("Bibliothek", "Library");
    public static string NavPacks => T("Icon-Packs", "Icon packs");
    public static string NavAssignments => T("Zuweisungen & Sicherung", "Assignments & backup");
    public static string Settings => T("Einstellungen", "Settings");
    public static string NavToggle => T("Navigation ein- oder ausklappen", "Expand or collapse navigation");

    // ---------- Common ----------
    public static string Back => T("Zurück", "Back");
    public static string Cancel => T("Abbrechen", "Cancel");
    public static string Close => T("Schließen", "Close");
    public static string Ok => T("OK", "OK");
    public static string Delete => T("Entfernen", "Remove");
    public static string Rename => T("Umbenennen", "Rename");
    public static string OpenFolder => T("Öffnen", "Open");
    public static string ShowInExplorer => T("Im Explorer zeigen", "Show in Explorer");
    public static string CopyPath => T("Pfad kopieren", "Copy path");
    public static string Change => T("Ändern", "Change");
    public static string Search => T("Suchen", "Search");
    public static string ComingSoon => T("In Arbeit", "Coming soon");

    // ---------- Placeholder pages (later stages) ----------
    public static string ProgramsSubtitle => T("Verknüpfungen auf dem Desktop, im Startmenü und in der Taskleiste mit neuen Icons versehen.",
                                              "Give shortcuts on the desktop, in the Start menu and on the taskbar new icons.");
    public static string SystemIconsSubtitle => T("Dieser PC, Papierkorb, Netzwerk, Benutzerordner und weitere Windows-Symbole.",
                                                 "This PC, Recycle Bin, Network, user folder and other Windows icons.");
    public static string DrivesSubtitle => T("Eigene Icons für Laufwerke wie C:, D: oder USB-Sticks.", "Custom icons for drives like C:, D: or USB sticks.");
    public static string FoldersSubtitle => T("Benutzerordner und beliebige Ordner mit eigenen Icons.", "User folders and any folder with custom icons.");
    public static string FileTypesSubtitle => T("Icons für Dateitypen wie PDF, ZIP oder MP3.", "Icons for file types like PDF, ZIP or MP3.");
    public static string AssignmentsSubtitle => T("Alle Änderungen auf einen Blick, Sicherung, Export und Wiederherstellen.",
                                                 "All changes at a glance, backup, export and restore.");
    public static string PlannedStage(int stage) => T($"Kommt in Etappe {stage}", $"Coming in stage {stage}");

    // ---------- Library ----------
    public static string LibrarySubtitle => T("Alle Icons deiner Icon-Packs durchsuchen.", "Browse all icons of your icon packs.");
    public static string SearchIcons => T("Icons suchen", "Search icons");
    public static string AllPacks => T("Alle Icon-Packs", "All icon packs");
    public static string AllCategories => T("Alle Kategorien", "All categories");
    public static string NoCategory => T("Ohne Kategorie", "Uncategorized");
    public static string IconCount(int n) => T($"{n:N0} Icons", $"{n:N0} icons");
    public static string IconCountOf(int n, int total) => n == total ? IconCount(n) : T($"{n:N0} von {total:N0} Icons", $"{n:N0} of {total:N0} icons");
    public static string NoIconsFound => T("Keine passenden Icons gefunden.", "No matching icons found.");
    public static string NoPacksTitle => T("Noch keine Icon-Packs", "No icon packs yet");
    public static string NoPacksText => T("Importiere ein Icon-Pack: eine Android-APK, eine ZIP oder einen Ordner mit Bildern bzw. ICO-Dateien. Du kannst eine Datei auch einfach in dieses Fenster ziehen.",
                                         "Import an icon pack: an Android APK, a ZIP or a folder with images or ICO files. You can also simply drop a file onto this window.");
    public static string GoToPacks => T("Zu den Icon-Packs", "Go to icon packs");
    public static string TileSize => T("Kachelgröße", "Tile size");
    public static string TileSmall => T("Klein", "Small");
    public static string TileMedium => T("Mittel", "Medium");
    public static string TileLarge => T("Groß", "Large");
    public static string SelectedIcon => T("Ausgewähltes Icon", "Selected icon");
    public static string FromPack => T("Aus", "From");
    public static string Category => T("Kategorie", "Category");

    // ---------- Packs ----------
    public static string PacksSubtitle => T("Icon-Packs importieren und verwalten: Android-APKs, ZIPs und Ordner mit Bildern oder ICO-Dateien. Du kannst Icons aus beliebig vielen Packs mischen.",
                                           "Import and manage icon packs: Android APKs, ZIPs and folders with images or ICO files. You can mix icons from as many packs as you like.");
    public static string ImportApk => T("APK oder ZIP importieren", "Import APK or ZIP");
    public static string ImportFolder => T("Ordner importieren", "Import folder");
    public static string ImportApkTip => T("Android-Icon-Pack (.apk, .apks, .xapk) oder ZIP mit Bildern", "Android icon pack (.apk, .apks, .xapk) or ZIP with images");
    public static string ImportFolderTip => T("Ordner mit PNG-, ICO-, WebP- oder JPG-Dateien; Unterordner werden zu Kategorien", "Folder with PNG, ICO, WebP or JPG files; subfolders become categories");
    public static string Importing => T("Wird importiert …", "Importing …");
    public static string ImportFailed => T("Import fehlgeschlagen", "Import failed");
    public static string ImportDone(string name, int count) => T($"„{name}“ importiert: {count:N0} Icons.", $"“{name}” imported: {count:N0} icons.");
    public static string ImportSkipped(int missing, int unsupported, int failed)
    {
        var parts = new List<string>();
        if (missing > 0) parts.Add(T($"{missing:N0} ohne Bilddatei", $"{missing:N0} without an image file"));
        if (unsupported > 0) parts.Add(T($"{unsupported:N0} nur als Vektorgrafik", $"{unsupported:N0} only as vector graphics"));
        if (failed > 0) parts.Add(T($"{failed:N0} nicht lesbar", $"{failed:N0} unreadable"));
        return parts.Count == 0 ? "" : T("Übersprungen: ", "Skipped: ") + string.Join(", ", parts) + ".";
    }
    public static string Browse => T("Durchsuchen", "Browse");
    public static string PackImported => T("Importiert", "Imported");
    public static string PackSource => T("Quelle", "Source");
    public static string PackVersion => T("Version", "Version");
    public static string PackCategories(int n) => T(n == 1 ? "1 Kategorie" : $"{n} Kategorien", n == 1 ? "1 category" : $"{n} categories");
    public static string RenamePackTitle => T("Icon-Pack umbenennen", "Rename icon pack");
    public static string DeletePackTitle => T("Icon-Pack entfernen?", "Remove icon pack?");
    public static string DeletePackText(string name) => T($"„{name}“ wird aus der Icon Werkstatt entfernt. Die Originaldatei bleibt unverändert.",
                                                         $"“{name}” will be removed from Icon Workshop. The original file stays untouched.");
    public static string DeletePackInUse(int n) => T($"{n} Zuweisung(en) nutzen noch Icons aus diesem Pack. Diese ICO-Dateien bleiben erhalten, damit die Icons weiter angezeigt werden.",
                                                    $"{n} assignment(s) still use icons from this pack. Those ICO files are kept so the icons keep showing.");
    public static string DropHint => T("Zum Importieren loslassen", "Release to import");
    public static string Name => T("Name", "Name");

    // ---------- Settings ----------
    public static string General => T("Allgemein", "General");
    public static string GeneralSubtitle => T("Programminfo, Speicherorte und Sprache", "App info, locations and language");
    public static string Help => T("Hilfe", "Help");
    public static string HelpSubtitle => T("So nutzt du Icon-Packs, Bilder und eigene Icons unter Windows", "How to use icon packs, images and your own icons on Windows");
    public static string Licenses => T("Lizenzen", "Licenses");
    public static string Privacy => T("Datenschutz", "Privacy");
    public static string LicensesNav => T("Lizenzen & Datenschutz", "Licenses & privacy");
    public static string LicensesSubtitle => T("Verwendete Komponenten und wie die App mit deinen Daten umgeht", "Components used and how the app handles your data");
    public static string LicenseText => T("Lizenztext", "License text");
    public static string Support => T("Unterstützung", "Support");
    public static string SupportSubtitle => T("Wenn dir die Icon Werkstatt gefällt", "If you like Icon Workshop");
    public static string SupportHeadline => T("Kostenlos, und das bleibt so.", "Free, and it stays that way.");
    public static string SupportText => T(
        "Die Icon Werkstatt ist kostenlos und Open Source, ohne Werbung, ohne Abo und ohne Datensammlung. " +
        "Wenn sie deinem Desktop einen frischen, einheitlichen Look gibt, freue ich mich über einen Kaffee. " +
        "Jede Unterstützung hilft, die Icon Werkstatt weiterzuentwickeln.",
        "Icon Workshop is free and open source, with no ads, no subscription and no data collection. " +
        "If it gives your desktop a fresh, consistent look, I'd be happy about a coffee. " +
        "Every bit of support helps keep Icon Workshop going.");
    public static string BuyCoffee => T("Spendiere mir einen Kaffee", "Buy me a coffee");
    public static string SupportVoluntary => T("Freiwillig: Alle Funktionen stehen allen kostenlos zur Verfügung.",
                                               "Voluntary: every feature is free for everyone.");
    public static string MoreWays => T("Auch so hilfst du weiter", "Other ways to help");
    public static string ShareTitle => T("Weiterempfehlen", "Spread the word");
    public static string ShareText => T("Erzähl Freunden und Kollegen, die ihren Desktop gern gestalten, von der Icon Werkstatt.",
                                        "Tell friends and colleagues who like to customize their desktop about Icon Workshop.");
    public static string StarTitle => T("Stern auf GitHub", "Star on GitHub");
    public static string StarText => T("Ein Stern für die Icon Werkstatt auf GitHub hilft anderen, das Projekt zu finden.",
                                       "A star for Icon Workshop on GitHub helps others find the project.");
    public static string StarLink => T("Icon Werkstatt auf GitHub", "Icon Workshop on GitHub");
    public static string FeedbackTitle => T("Feedback geben", "Give feedback");
    public static string FeedbackText => T("Ideen und Fehlermeldungen, gern als Issue auf GitHub, machen die Icon Werkstatt besser.",
                                           "Ideas and bug reports, ideally as a GitHub issue, make Icon Workshop better.");
    public static string FeedbackLink => T("Issue erstellen", "Open an issue");
    public static string VersionLabel => T("Version", "Version");
    public static string AuthorLine => T($"{Author} • Kostenlos und ohne Werbung", $"{Author} • Free and without ads");
    public static string Folders => T("Speicherorte", "Locations");
    public static string DataFolderLabel => T("Datenordner", "Data folder");
    public static string PacksFolderLabel => T("Icon-Packs", "Icon packs");
    public static string PacksFolderDesc => T("Zugewiesene Icons verweisen auf diesen Ordner. Beim Ändern werden alle Packs verschoben und die Zuweisungen angepasst.",
                                             "Assigned icons point to this folder. When changing it, all packs are moved and assignments are updated.");
    public static string ProgramFolderLabel => T("Programmordner", "Program folder");
    public static string ChoosePacksFolder => T("Neuen Ordner für die Icon-Packs wählen", "Choose a new folder for the icon packs");
    public static string MovePacksTitle => T("Icon-Packs verschieben?", "Move icon packs?");
    public static string MovePacksText(string path) => T($"Alle Icon-Packs werden nach „{path}“ verschoben.", $"All icon packs will be moved to “{path}”.");
    public static string Move => T("Verschieben", "Move");
    public static string MoveFailed => T("Verschieben fehlgeschlagen", "Moving failed");
    public static string Options => T("Optionen", "Options");
    public static string OptLanguageTitle => T("Sprache", "Language");
    public static string OptLanguageDesc => T("Standard ist die Anzeigesprache von Windows. Andere Sprachen fallen auf Englisch zurück.",
                                             "Default is the Windows display language. Other languages fall back to English.");
    public static string LangAuto => T("Automatisch (Windows)", "Automatic (Windows)");
    public static string RestartNeeded => T("Die neue Sprache gilt nach einem Neustart der App.", "The new language applies after restarting the app.");
    public static string RestartNow => T("Jetzt neu starten", "Restart now");
    public static string OptThemeTitle => T("Darstellung", "Appearance");
    public static string OptThemeDesc => T("Hell oder dunkel und die Akzentfarbe folgen automatisch den Windows-Einstellungen.",
                                          "Light or dark mode and the accent color automatically follow your Windows settings.");
    public static string OptExplorerMenuTitle => T("Im Explorer-Kontextmenü anbieten", "Offer in the Explorer context menu");
    public static string OptExplorerMenuDesc => T("Rechtsklick auf eine Verknüpfung, einen Ordner oder ein Laufwerk: „Icon ändern mit Icon Werkstatt“ öffnet direkt die passende Seite. Unter Windows 11 steht der Eintrag unter „Weitere Optionen anzeigen“ (Umschalt+F10). Gilt nur für dein Konto; Ausschalten entfernt den Eintrag wieder.",
                                                   "Right-click a shortcut, folder or drive: “Change icon with Icon Workshop” opens the right page directly. On Windows 11 the entry is under “Show more options” (Shift+F10). Applies only to your account; switching it off removes the entry again.");
    public static string OptThumbFrameTitle => T("Rahmen um PNG- und ICO-Vorschauen ausblenden", "Hide frame around PNG and ICO previews");
    public static string OptThumbFrameDesc => T("Windows legt um Vorschaubilder von Bilddateien einen grauen Fotorahmen, auch um exportierte Icons auf dem Desktop. Zugewiesene Icons sind nicht betroffen. Gilt nur für dein Konto; Ausschalten stellt den Windows-Standard wieder her. Wirkt nach F5 im Explorer.",
                                                "Windows draws a gray photo frame around thumbnails of image files, also around exported icons on the desktop. Assigned icons are not affected. Applies only to your account; turning it off restores the Windows default. Takes effect after F5 in Explorer.");
    public static string OpenWindowsColors => T("Windows-Farben", "Windows colors");
    public static string LinkFollows => T("Link folgt", "Link coming soon");

    // ---------- Color picker ----------
    public static string T_Color => T("Farbe wählen", "Choose color");
    public static string T_Opacity => T("Deckkraft", "Opacity");
    public static string T_Suggestions => T("Vorschläge", "Suggestions");
    public static string T_Recent => T("Zuletzt verwendet", "Recently used");
    public static string T_HexValue => T("Hex-Wert", "Hex value");
}
