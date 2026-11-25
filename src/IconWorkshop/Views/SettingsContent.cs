namespace IconWorkshop.Views;

/// <summary>List item: optional bold caption and description.</summary>
public sealed record HelpItem(string? Label, string Text)
{
    public bool HasLabel => Label is not null;
}

/// <summary>Expandable section: intro, list, closing (each optional).</summary>
public sealed record HelpTopic(string Glyph, string Title, string Summary, string? Intro, IReadOnlyList<HelpItem> Items, string? Outro)
{
    public bool HasIntro => Intro is not null;
    public bool HasItems => Items.Count > 0;
    public bool HasOutro => Outro is not null;
}

public sealed record LicenseEntry(string Glyph, string Name, string Version, string License, string Copyright, string? Note, string? Text)
{
    public bool HasText => Text is not null;
    public bool HasNote => Note is not null;
    public string Meta => (Version.Length > 0 && char.IsDigit(Version[0]) ? $"{Loc.VersionLabel} {Version}" : Version) + $"  ·  {License}";
}

/// <summary>Content of the settings pages (bilingual).</summary>
public static class SettingsContent
{
    private static HelpItem I(string? labelDe, string? labelEn, string de, string en) =>
        new(labelDe is null ? null : Loc.T(labelDe, labelEn!), Loc.T(de, en));

    public static IReadOnlyList<HelpTopic> HelpTopics() =>
    [
        new("",
            Loc.T("Worum es geht", "What it's for"),
            Loc.T("Icons aus Icon-Packs, Bildern und eigenen Entwürfen unter Windows nutzen", "Use icons from icon packs, images and your own designs on Windows"),
            Loc.T("Die Icon Werkstatt macht Icons aus verschiedenen Quellen unter Windows nutzbar: für Programme, Systemsymbole, Laufwerke, Ordner und Dateitypen.",
                  "Icon Workshop makes icons from different sources usable on Windows: for programs, system icons, drives, folders and file types."),
            [
                I("Android-Icon-Packs", "Android icon packs", "Für Android gibt es tausende liebevoll gestaltete Icon-Packs. Die App liest sie direkt aus der APK-Datei, samt Namen, Kategorien und App-Zuordnungen.",
                  "There are thousands of lovingly designed icon packs for Android. The app reads them directly from the APK file, including names, categories and app mappings."),
                I("Bilder und Icon-Sammlungen", "Images and icon collections", "Jeder Ordner oder jede ZIP mit PNG-, ICO-, WebP-, JPG- oder BMP-Dateien wird zum Pack, z. B. Icon-Sets von Windows-Designern oder eigene Bilder.",
                  "Any folder or ZIP with PNG, ICO, WebP, JPG or BMP files becomes a pack, e.g. icon sets by Windows designers or your own images."),
                I("Eigene Icons", "Your own icons", "Im Icon-Maker baust du Icons selbst, im Stil eines Packs oder ganz frei.", "In the icon maker you build icons yourself, in the style of a pack or completely free."),
                I("Mehrere Packs", "Several packs", "Du kannst beliebig viele Packs importieren und Icons frei mischen.", "You can import as many packs as you like and mix icons freely."),
                I("Nur für dich", "Just for you", "Änderungen gelten für dein Benutzerkonto. Administratorrechte sind normalerweise nicht nötig.", "Changes apply to your user account. Admin rights are normally not needed."),
                I("Immer umkehrbar", "Always reversible", "Vor jeder Änderung wird das Original gesichert.", "The original is backed up before every change."),
            ],
            null),

        new("",
            Loc.T("Icon-Packs importieren", "Importing icon packs"),
            Loc.T("APK, ZIP oder Ordner mit Bildern", "APK, ZIP or a folder with images"),
            Loc.T("Unter Icon-Packs auf „APK oder ZIP importieren“ oder „Ordner importieren“ klicken, oder die Datei einfach ins Fenster ziehen.",
                  "On Icon packs, click “Import APK or ZIP” or “Import folder”, or simply drop the file onto the window."),
            [
                I("APK", "APK", "Android-Icon-Packs (.apk, auch .apks und .xapk). Namen, Kategorien und Zuordnungen werden aus dem Pack übernommen.",
                  "Android icon packs (.apk, also .apks and .xapk). Names, categories and mappings are taken from the pack."),
                I("Ordner oder ZIP", "Folder or ZIP", "PNG, ICO, WebP, JPG oder BMP, aus jeder Quelle. Unterordner werden zu Kategorien, ICO-Dateien bleiben mit allen Größen erhalten.",
                  "PNG, ICO, WebP, JPG or BMP, from any source. Subfolders become categories, ICO files keep all their sizes."),
                I("Speicherplatz", "Disk space", "Gespeichert werden nur die Vorschaubilder. Die Windows-Icon-Datei (ICO) entsteht erst, wenn du ein Icon zuweist.",
                  "Only the preview images are stored. The Windows icon file (ICO) is only created when you assign an icon."),
            ],
            Loc.T("Die Originaldatei (APK, ZIP oder Ordner) wird nicht verändert und wird danach nicht mehr gebraucht.", "The original (APK, ZIP or folder) is not modified and is no longer needed afterwards.")),

        new("",
            Loc.T("Bibliothek", "Library"),
            Loc.T("Icons suchen und ansehen", "Search and view icons"),
            null,
            [
                I("Suche", "Search", "Strg+F springt ins Suchfeld. Gesucht wird im Icon-Namen, z. B. „chrome“ oder „file manager“.",
                  "Ctrl+F jumps to the search box. The icon name is searched, e.g. “chrome” or “file manager”."),
                I("Filter", "Filter", "Nach Icon-Pack und Kategorie filtern, die Kachelgröße ist einstellbar.", "Filter by icon pack and category, the tile size is adjustable."),
                I("Zuweisen an …", "Assign to …", "Icon auswählen, „Zuweisen an …“ klicken und den Bereich wählen. Dort das Ziel anklicken: Das Icon ist schon ausgewählt.",
                  "Select an icon, click “Assign to …” and choose the area. Click the target there: the icon is already selected."),
            ],
            null),

        new("",
            Loc.T("Programme", "Programs"),
            Loc.T("Verknüpfungen mit neuen Icons versehen", "Give shortcuts new icons"),
            Loc.T("Unter Programme ein Programm anklicken, ein Icon auswählen und „Icon zuweisen“ klicken. Ein Doppelklick auf ein Icon weist es direkt zu.",
                  "On Programs, click a program, choose an icon and click “Assign icon”. Double-clicking an icon assigns it right away."),
            [
                I("Alle Verknüpfungen gemeinsam", "All shortcuts together", "Hat ein Programm Verknüpfungen auf dem Desktop, im Startmenü und in der Taskleiste, werden alle angehakten zusammen geändert.",
                  "If a program has shortcuts on the desktop, in the Start menu and on the taskbar, all checked ones are changed together."),
                I("Schloss-Symbol", "Lock symbol", "Verknüpfungen aller Benutzer brauchen Administratorrechte. Windows fragt dann einmal nach; alle anderen Änderungen laufen ohne.",
                  "Shortcuts of all users need admin rights. Windows then asks once; all other changes work without."),
                I("Ohne Verknüpfung", "Without shortcut", "Für Windows-Apps und Programme ohne Verknüpfung legt die App eine eigene auf dem Desktop und/oder im Startmenü (Ordner Icon Workshop) an.",
                  "For Windows apps and programs without a shortcut, the app creates its own on the desktop and/or in the Start menu (folder Icon Workshop)."),
                I("Wiederherstellen", "Restore", "„Original wiederherstellen“ setzt das vorherige Icon zurück. Eigene Verknüpfungen werden dabei wieder entfernt.",
                  "“Restore original” brings back the previous icon. Own shortcuts are removed again."),
                I("Taskleiste", "Taskbar", "Läuft ein Programm, zeigt die Taskleiste oft das Icon des laufenden Programms. Das lässt sich ohne Eingriff in das Programm nicht ändern. Angeheftete Icons aktualisieren sich manchmal erst nach dem Neuanheften.",
                  "While a program runs, the taskbar often shows the running program's icon. That can't be changed without modifying the program. Pinned icons sometimes only update after re-pinning."),
            ],
            null),

        new("",
            Loc.T("Systemsymbole und Laufwerke", "System icons and drives"),
            Loc.T("Dieser PC, Papierkorb, Netzwerk, Laufwerksbuchstaben", "This PC, Recycle Bin, Network, drive letters"),
            Loc.T("Diese Icons werden nur für dein Benutzerkonto geändert, ohne Administratorrechte. Das vorherige Icon wird gemerkt und lässt sich jederzeit wiederherstellen.",
                  "These icons are changed only for your user account, without admin rights. The previous icon is remembered and can be restored at any time."),
            [
                I("Papierkorb", "Recycle Bin", "Leer und voll werden getrennt eingestellt.", "Empty and full are set separately."),
                I("Laufwerke", "Drives", "Das Icon hängt am Laufwerksbuchstaben. Für einen USB-Stick, der gerade nicht steckt, den Buchstaben über „Laufwerksbuchstaben hinzufügen“ ergänzen.",
                  "The icon belongs to the drive letter. For a USB stick that is not plugged in, add its letter via “Add drive letter”."),
                I("Aktualisierung", "Refresh", "Windows übernimmt neue Systemsymbole manchmal erst nach F5 auf dem Desktop oder einer neuen Anmeldung.",
                  "Windows sometimes only shows new system icons after pressing F5 on the desktop or signing in again."),
            ],
            null),

        new("",
            Loc.T("Ordner und Dateitypen", "Folders and file types"),
            Loc.T("Benutzerordner, eigene Ordner, PDF, ZIP, MP3 …", "User folders, own folders, PDF, ZIP, MP3 …"),
            null,
            [
                I("Ordner", "Folders", "Das Icon steht in einer versteckten desktop.ini im Ordner, wie bei Windows selbst. Eine vorhandene desktop.ini (z. B. bei Dokumente oder Downloads) wird vorher gesichert und beim Wiederherstellen byte-genau zurückgeschrieben. Weitere Ordner über „Ordner hinzufügen“.",
                  "The icon is stored in a hidden desktop.ini in the folder, like Windows does it. An existing desktop.ini (e.g. for Documents or Downloads) is backed up first and written back exactly when restoring. More folders via “Add folder”."),
                I("Dateitypen", "File types", "Das Icon wird für das Programm gesetzt, das den Typ gerade öffnet, nur für dein Konto. Nach einem Wechsel des Standardprogramms das Icon erneut zuweisen. Weitere Endungen über das Eingabefeld hinzufügen.",
                  "The icon is set for the program that currently opens the type, only for your account. After changing the default app, assign the icon again. Add more extensions via the input box."),
                I("Nicht änderbar", "Not changeable", "Typen, deren Dateien ihr eigenes Icon zeigen (z. B. .exe), lassen sich nicht pro Typ ändern; das wird angezeigt.",
                  "Types whose files show their own icon (e.g. .exe) can't be changed per type; this is shown."),
            ],
            null),

        new("",
            Loc.T("Zuweisungen & Sicherung", "Assignments & backup"),
            Loc.T("Überblick, erneut anwenden, Export, Cache", "Overview, re-apply, export, cache"),
            null,
            [
                I("Überblick", "Overview", "Alle Änderungen mit Icon, Pack und Datum. „Überschrieben“ heißt, dass etwas anderes das Icon ersetzt hat; „Nicht mehr vorhanden“, dass das Ziel gelöscht wurde.",
                  "All changes with icon, pack and date. “Overwritten” means something else replaced the icon; “No longer exists” means the target was deleted."),
                I("Erneut anwenden", "Re-apply", "Setzt überschriebene Icons wieder, z. B. nach einem Programm-Update. Optional einmal still bei jeder Anmeldung.",
                  "Sets overwritten icons again, e.g. after a program update. Optionally once silently at every sign-in."),
                I("Export und Import", "Export and import", "Speichert alle Zuweisungen samt ICO-Dateien in einer ZIP, etwa für eine Neuinstallation. Beim Import werden die Originale dieses PCs wie gewohnt gesichert.",
                  "Saves all assignments including the ICO files in a ZIP, e.g. for a reinstall. On import, the originals of this PC are backed up as usual."),
                I("Cache leeren", "Clear cache", "Löscht den Icon- und Miniaturansichten-Cache von Windows. Dafür startet der Explorer kurz neu; vorher wird nachgefragt.",
                  "Deletes Windows' icon and thumbnail cache. Explorer restarts briefly for this; you are asked first."),
            ],
            null),

        new("",
            Loc.T("Automatische Vorschläge", "Automatic suggestions"),
            Loc.T("Viele Programme in einem Schritt", "Many programs in one step"),
            Loc.T("Unter Programme auf „Vorschläge“ klicken. Die App sucht für jedes Programm mit Verknüpfung ein passendes Icon im gewählten Pack.",
                  "On Programs, click “Suggestions”. The app looks for a matching icon in the chosen pack for every program with a shortcut."),
            [
                I("Wie gesucht wird", "How it searches", "Über den Programmnamen, den Namen der EXE und den Icon-Namen; bei Android-Packs zusätzlich über die App-Namen, die das Pack kennt. Allgemeine Wörter wie „Manager“ oder „Player“ zählen nicht, damit keine Zufallstreffer entstehen.",
                  "By the program name, the EXE name and the icon name; for Android packs also by the app names the pack knows. General words like “Manager” or “Player” don't count, so there are no random matches."),
                I("Prüfen", "Review", "Sichere Treffer sind vorausgewählt. Häkchen setzen oder entfernen; über das Stift-Symbol wählst du für ein Programm ein anderes Icon.",
                  "Confident matches are pre-selected. Check or uncheck; the pencil symbol lets you choose another icon for a program."),
                I("Lücken füllen", "Fill gaps", "Programme, für die das Pack kein Icon hat, bekommen ihr eigenes Icon im Stil des Packs (Kachel, Form und Rahmen des Packs, sonst eine Kachel in der Farbe des Programms). Diese sind nicht vorausgewählt; über das Blitz-Symbol passt du eines im 1-Klick-Generator an.",
                  "Programs the pack has no icon for get their own icon in the pack's style (the pack's plate, shape and frame, otherwise a tile in the program's color). These are not pre-selected; the lightning symbol lets you adjust one in the one-click generator."),
                I("Übernehmen", "Apply", "Alles wird in einem Schritt geändert, Verknüpfungen aller Benutzer mit höchstens einer Adminabfrage. Rückgängig unter „Zuweisungen & Sicherung“.",
                  "Everything is changed in one step, shortcuts of all users with at most one admin prompt. Undo on “Assignments & backup”."),
            ],
            null),

        new("",
            Loc.T("Kontextmenü, Spiele und Profile", "Context menu, games and profiles"),
            Loc.T("Schneller ans Ziel, Steam-Icons, Looks wechseln", "Faster to the target, Steam icons, switching looks"),
            Loc.T("Drei Abkürzungen für den Alltag.", "Three shortcuts for everyday use."),
            [
                I("Explorer-Kontextmenü", "Explorer context menu", "Unter Allgemein > Optionen einschalten. Dann bietet ein Rechtsklick auf eine Verknüpfung, einen Ordner oder ein Laufwerk „Icon ändern mit Icon Werkstatt“ an; die App öffnet direkt die passende Seite. Unter Windows 11 steht der Eintrag unter „Weitere Optionen anzeigen“ (Umschalt+F10), weil das kurze Menü nur installierte Store-Pakete aufnimmt.",
                  "Switch it on under General > Options. A right-click on a shortcut, folder or drive then offers “Change icon with Icon Workshop”; the app opens the right page directly. On Windows 11 the entry is under “Show more options” (Shift+F10), because the short menu only takes installed Store packages."),
                I("Spiele", "Games", "Verknüpfungen von Steam und dem Epic Games Launcher werden erkannt (Filter „Spiele“). Steam legt Desktop-Verknüpfungen oft mit fehlender Icon-Datei an, Windows zeigt dann ein leeres Blatt; der Filter „Icon fehlt“ findet sie. Die Liste zeigt dafür Steams kleines Spiel-Icon, und „Icon erzeugen“ öffnet den Generator mit der Kategorie Spiele in den Farben des Spiels.",
                  "Shortcuts of Steam and the Epic Games Launcher are recognized (filter “Games”). Steam often creates desktop shortcuts with a missing icon file, so Windows shows a blank page; the filter “Icon missing” finds them. The list shows Steam's small game icon for them, and “Generate icon” opens the generator with the games category in the game's colors."),
                I("Profile", "Profiles", "Unter „Zuweisungen & Sicherung“ speicherst du den aktuellen Stand als Profil, z. B. „Dunkel“ oder „Gaming“. „Anwenden“ stellt die aktuellen Zuweisungen wieder her und setzt die des Profils. Profile sind Exporte im Datenordner und funktionieren auch, wenn ein Pack inzwischen gelöscht ist.",
                  "Under “Assignments & backup” you save the current state as a profile, e.g. “Dark” or “Gaming”. “Apply” restores the current assignments and sets those of the profile. Profiles are exports in the data folder and keep working even if a pack has been deleted since."),
            ],
            null),

        new("",
            Loc.T("Icon-Maker", "Icon maker"),
            Loc.T("Eigene Icons bauen, frei oder im Stil eines Packs", "Build your own icons, freely or in the style of a pack"),
            Loc.T("Mit Ebenen, Formen, Farben, Verläufen und Effekten. Android-Icon-Packs bringen zusätzlich Bausteine für Apps mit, die sie nicht abdecken: eine Kachel als Hintergrund, eine Form (Maske) und einen Rahmen. Der Icon-Maker nutzt genau diese; bei anderen Packs leitet er die Form aus den Icons ab.",
                  "With layers, shapes, colors, gradients and effects. Android icon packs also include building blocks for apps they don't cover: a plate as background, a shape (mask) and a frame. The icon maker uses exactly these; for other packs it derives the shape from the icons."),
            [
                I("1-Klick-Generator", "One-click generator", "Einen Namen eingeben (z. B. „Steam“, „Musik“, „Backup“) oder ein Programm wählen: Es erscheinen 18 fertige Varianten mit Markenlogo, passendem Symbol, Initialen oder dem Programm-Icon, in abgestimmten Farben und verschiedenen Looks. Ein Klick übernimmt eine Variante zum Speichern oder Feinschliff. „Würfeln“ zeigt neue Varianten; angehakte Teile (Inhalt, Farben, Form, Look) bleiben dabei. Aus einem Programm heraus öffnet „Icon erzeugen“ den Generator direkt.",
                  "Enter a name (e.g. “Steam”, “music”, “backup”) or choose a program: 18 finished variants appear with a brand logo, a matching symbol, initials or the program icon, in matching colors and different looks. One click takes a variant for saving or fine-tuning. “Roll” shows new variants; checked parts (content, colors, shape, look) stay. From a program, “Generate icon” opens the generator directly."),
                I("Kategorien und Windows-Formen", "Categories and Windows shapes", "Im Generator wählst du eine Kategorie: Programme, Spiele, Tools, Büro, Medien, System bringen passende Formen, Farben und Looks mit; Ordner, Laufwerk und Dateityp nutzen eigene Formen wie unter Windows (Ordner mit Reiter, Laufwerk mit Leuchte, Seite mit Eselsohr samt Endung wie „PSD“). Bei Laufwerken, Ordnern und Dateitypen öffnet „Icon erzeugen“ den Generator gleich mit der richtigen Kategorie.",
                  "In the generator you choose a category: programs, games, tools, office, media and system bring matching shapes, colors and looks; folder, drive and file type use their own Windows-like shapes (folder with tab, drive with light, page with folded corner and an extension like “PSD”). For drives, folders and file types, “Generate icon” opens the generator with the right category."),
                I("Serie", "Series", "„Serie …“ macht aus der gewählten Variante viele Icons im selben Stil: ein Name pro Zeile oder Programme aus der Liste, das Symbol passt sich jedem Namen an, auf Wunsch mit abwechselnden Farben. Alle landen in „Eigene Icons“.",
                  "“Series …” turns the selected variant into many icons in the same style: one name per line or programs from the list, the symbol follows each name, optionally with alternating colors. All of them go into “My icons”."),
                I("Farbpaletten", "Color palettes", "Unter Kachel > Farbpalette setzt ein Klick einen abgestimmten Verlauf als Hintergrund (Kräftig, Fluent, Pastell, Neon, Grautöne, Erdtöne); die Inhaltsfarbe passt sich an, damit alles lesbar bleibt.",
                  "Under Tile > Color palette one click sets a matching gradient as background (vivid, Fluent, pastel, neon, grays, earth tones); the content color adapts so everything stays readable."),
                I("Inhalt", "Content", "Das Icon eines Programms, ein eigenes Bild, ein Pack-Icon, ein Symbol (Windows, Material Symbols, Tabler Icons, Font Awesome Free; durchsuchbar, auch auf Deutsch) oder kurzer Text. Bilder lassen sich einfarbig darstellen, passend zu einfarbigen Packs.",
                  "The icon of a program, your own image, a pack icon, a symbol (Windows, Material Symbols, Tabler Icons, Font Awesome Free; searchable) or short text. Images can be shown in one color, matching single-color packs."),
                I("Hintergrund", "Background", "Die Kachel des Packs oder dieselbe Form in eigener Farbe bzw. als Verlauf. Packs ohne eigene Kachel: Die App lernt die gemeinsame Form aus den Icons des Packs; haben die Icons keine einheitliche Form, gibt es eine neutrale, abgerundete Kachel.",
                  "The pack's plate or the same shape in a custom color or gradient. Packs without a plate: the app learns the common shape from the pack's icons; if the icons have no uniform shape, a neutral rounded tile is used."),
                I("Speichern", "Saving", "Fertige Icons landen im Pack „Eigene Icons“, erscheinen in der Bibliothek, lassen sich überall zuweisen und werden mit exportiert.",
                  "Finished icons go into the “My icons” pack, show up in the library, can be assigned everywhere and are included in exports."),
                I("Export als PNG/ICO", "Export as PNG/ICO", "Die Dateien sind rundum transparent. Den grauen Rahmen, den der Explorer um Bild-Vorschauen legt, kannst du unter Allgemein > Optionen ausblenden.",
                  "The files are fully transparent around the edges. The gray frame Explorer draws around image previews can be hidden under General > Options."),
                I("Ältere Importe", "Older imports", "Für Packs, die vor dem Icon-Maker importiert wurden, liest die App den Stil aus der Original-APK nach, sofern sie noch am alten Ort liegt. Sonst das Pack einmal neu importieren.",
                  "For packs imported before the icon maker existed, the app reads the style from the original APK if it is still in its old place. Otherwise import the pack once more."),
            ],
            null),
    ];

    public static IReadOnlyList<LicenseEntry> Licenses() =>
    [
        new("", ".NET / WPF", Environment.Version.ToString(), "MIT", "Copyright (c) .NET Foundation and Contributors",
            Loc.T("Laufzeit und Oberfläche (Windows Presentation Foundation mit Fluent-Design).", "Runtime and user interface (Windows Presentation Foundation with Fluent design)."),
            MitText("Copyright (c) .NET Foundation and Contributors")),
        new("", "Segoe Fluent Icons", Loc.T("Teil von Windows", "Part of Windows"), Loc.T("Windows-Systemschrift", "Windows system font"), "© Microsoft Corporation",
            Loc.T("Symbole der Oberfläche. Die Schrift wird von Windows bereitgestellt und nicht mitgeliefert.", "User interface symbols. The font is provided by Windows and not shipped with the app."),
            null),
        new("", "Material Symbols (Rounded)", "Google Fonts", "Apache 2.0", "Copyright Google LLC",
            Loc.T("Symbole im Icon-Maker. Mitgeliefert; frei nutzbar und weitergebbar, auch kommerziell.", "Symbols in the icon maker. Shipped with the app; free to use and redistribute, also commercially."),
            IconWorkshop.Imaging.IconFonts.License("LICENSE-MaterialSymbols.txt")),
        new("", "Tabler Icons", "3.x", "MIT", "Copyright (c) 2020-2026 Paweł Kuna",
            Loc.T("Symbole im Icon-Maker. Mitgeliefert; frei nutzbar und weitergebbar, auch kommerziell.", "Symbols in the icon maker. Shipped with the app; free to use and redistribute, also commercially."),
            IconWorkshop.Imaging.IconFonts.License("LICENSE-Tabler.txt")),
        new("", "Font Awesome Free", "6.x", "SIL OFL 1.1 / CC BY 4.0", "© Fonticons, Inc. (https://fontawesome.com)",
            Loc.T("Symbole im Icon-Maker (Solid, Regular, Brands). Schriften unter SIL OFL 1.1, Symbole unter CC BY 4.0. Markenlogos (Brands) gehören ihren Inhabern und dürfen nur gemäß deren Markenrichtlinien verwendet werden.",
                  "Symbols in the icon maker (Solid, Regular, Brands). Fonts under SIL OFL 1.1, icons under CC BY 4.0. Brand logos belong to their owners and may only be used according to their trademark guidelines."),
            IconWorkshop.Imaging.IconFonts.License("LICENSE-FontAwesome.txt")),
        new("", Loc.T("Importierte Icon-Packs", "Imported icon packs"), Loc.T("Je nach Pack", "Depends on the pack"), Loc.T("Lizenz des jeweiligen Packs", "License of the respective pack"),
            Loc.T("© die jeweiligen Gestalter", "© the respective designers"),
            Loc.T("Icon-Packs gehören ihren Gestaltern. Nutze sie nur im Rahmen ihrer Lizenz, in der Regel für den persönlichen Gebrauch, und gib sie nicht weiter.",
                  "Icon packs belong to their designers. Only use them within their license, usually for personal use, and don't redistribute them."),
            null),
    ];

    public static HelpTopic Privacy() => new("",
        Loc.T("Deine Daten bleiben auf deinem PC", "Your data stays on your PC"),
        Loc.T("Kein Konto, keine Telemetrie, keine Internetverbindung", "No account, no telemetry, no internet connection"),
        null,
        [
            I("Lokal", "Local", "Icon-Packs, Sicherungen und Einstellungen liegen ausschließlich in deinem Benutzerprofil.", "Icon packs, backups and settings are stored only in your user profile."),
            I("Offline", "Offline", "Die App stellt keine Verbindung ins Internet her. Nur der Link auf der Seite Unterstützung öffnet deinen Browser, wenn du ihn anklickst.",
              "The app does not connect to the internet. Only the link on the Support page opens your browser when you click it."),
            I("Entfernen", "Removing", "Zum vollständigen Entfernen den Datenordner löschen (Einstellungen > Allgemein > Speicherorte). Vorher zugewiesene Icons wiederherstellen.",
              "To remove everything, delete the data folder (Settings > General > Locations). Restore assigned icons first."),
        ],
        null);

    private static string MitText(string copyright) =>
        "MIT License\n\n" + copyright + "\n\n" +
        "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), " +
        "to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, " +
        "and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\n" +
        "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\n" +
        "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, " +
        "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER " +
        "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";
}
