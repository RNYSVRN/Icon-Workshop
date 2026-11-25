namespace IconWorkshop.Imaging;

/// <summary>
/// Names of common Segoe Fluent Icons symbols (the font itself contains no names) plus German search
/// words, so symbols can be found by "home", "Haus", "Ordner", "music" ... Symbols without a name stay
/// visible in the full list.
/// </summary>
public static class GlyphNames
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [0xE700] = "GlobalNavButton menu hamburger", [0xE701] = "Wifi", [0xE702] = "Bluetooth", [0xE703] = "Connect", [0xE704] = "InternetSharing hotspot",
        [0xE705] = "VPN", [0xE706] = "Brightness sun", [0xE707] = "MapPin location", [0xE708] = "QuietHours moon", [0xE709] = "Airplane flight",
        [0xE70A] = "Tablet", [0xE70B] = "QuickNote note", [0xE70C] = "RememberedDevice", [0xE70D] = "ChevronDown", [0xE70E] = "ChevronUp",
        [0xE70F] = "Edit pencil", [0xE710] = "Add plus", [0xE711] = "Cancel close x", [0xE712] = "More dots", [0xE713] = "Settings gear cog",
        [0xE714] = "Video camera", [0xE715] = "Mail email envelope", [0xE716] = "People group", [0xE717] = "Phone call", [0xE718] = "Pin",
        [0xE719] = "Shop store bag", [0xE71A] = "Stop", [0xE71B] = "Link chain", [0xE71C] = "Filter", [0xE71D] = "AllApps list",
        [0xE71E] = "Zoom magnifier", [0xE71F] = "ZoomOut", [0xE720] = "Microphone mic", [0xE721] = "Search magnifier", [0xE722] = "Camera photo",
        [0xE723] = "Attach paperclip", [0xE724] = "Send", [0xE725] = "SendFill", [0xE726] = "Walk", [0xE727] = "InPrivate",
        [0xE728] = "FavoriteList", [0xE72A] = "Forward arrow right", [0xE72B] = "Back arrow left", [0xE72C] = "Refresh reload sync",
        [0xE72D] = "Share", [0xE72E] = "Lock padlock security", [0xE734] = "FavoriteStar star", [0xE735] = "FavoriteStarFill star",
        [0xE736] = "ReadingMode book", [0xE738] = "Remove minus", [0xE739] = "Checkbox", [0xE73E] = "CheckMark tick", [0xE740] = "FullScreen",
        [0xE748] = "SwitchUser", [0xE749] = "Print printer", [0xE74A] = "Up arrow", [0xE74B] = "Down arrow", [0xE74D] = "Delete trash bin",
        [0xE74E] = "Save disk floppy", [0xE74F] = "Mute", [0xE753] = "Cloud", [0xE754] = "Flashlight torch", [0xE755] = "RotationLock",
        [0xE756] = "CommandPrompt terminal console", [0xE75C] = "EraseTool eraser", [0xE75F] = "Dialpad", [0xE765] = "KeyboardClassic keyboard",
        [0xE767] = "Volume speaker sound", [0xE768] = "Play", [0xE769] = "Pause", [0xE76B] = "ChevronLeft", [0xE76C] = "ChevronRight",
        [0xE76D] = "InkingTool pen", [0xE76E] = "Emoji smile", [0xE770] = "System", [0xE771] = "Personalize paint brush", [0xE772] = "Devices",
        [0xE773] = "SearchAndApps", [0xE774] = "Globe world internet web", [0xE775] = "TimeLanguage", [0xE776] = "EaseOfAccess accessibility",
        [0xE777] = "UpdateRestore", [0xE778] = "HangUp", [0xE779] = "ContactInfo", [0xE77A] = "Unpin", [0xE77B] = "Contact person user",
        [0xE77C] = "Memo", [0xE77F] = "Paste clipboard", [0xE780] = "PhoneBook", [0xE781] = "LEDLight", [0xE783] = "Error",
        [0xE785] = "Unlock", [0xE786] = "Slideshow", [0xE787] = "Calendar date", [0xE789] = "Megaphone announcement", [0xE78A] = "Trim",
        [0xE78B] = "NewWindow", [0xE790] = "Color palette paint", [0xE793] = "Light", [0xE799] = "AspectRatio", [0xE7A6] = "Redo", [0xE7A7] = "Undo",
        [0xE7A8] = "Crop", [0xE7AC] = "OpenWith", [0xE7AD] = "Rotate", [0xE7B5] = "SetLockScreen", [0xE7B7] = "MapPin2", [0xE7B8] = "Package box",
        [0xE7BA] = "Warning alert", [0xE7BC] = "ReadingList", [0xE7BE] = "Education graduation", [0xE7BF] = "ShoppingCart cart",
        [0xE7C0] = "Train", [0xE7C1] = "Flag", [0xE7C3] = "Page document", [0xE7C4] = "TaskView", [0xE7C5] = "BrowsePhotos gallery",
        [0xE7C8] = "Record", [0xE7E6] = "Highlight marker", [0xE7E7] = "ActionCenterNotification", [0xE7E8] = "PowerButton power",
        [0xE7EC] = "DrivingMode car", [0xE7ED] = "RingerSilent", [0xE7EE] = "OtherUser", [0xE7EF] = "Admin shield", [0xE7F0] = "CC subtitles",
        [0xE7F1] = "SDCard memory", [0xE7F3] = "SettingsDisplaySound", [0xE7F4] = "TVMonitor monitor screen computer", [0xE7F5] = "Speakers",
        [0xE7F6] = "Headphone headphones music", [0xE7F7] = "DeviceLaptopPic laptop", [0xE7F8] = "DeviceLaptopNoPic laptop", [0xE7FC] = "Game controller gamepad",
        [0xE804] = "Car", [0xE805] = "Walk", [0xE806] = "Bus", [0xE80F] = "Home house", [0xE811] = "ParkingLocation", [0xE814] = "IncidentTriangle",
        [0xE815] = "Touch", [0xE816] = "MapDirections", [0xE81C] = "History clock", [0xE81D] = "Location", [0xE81E] = "MapLayers layers",
        [0xE821] = "Work briefcase", [0xE822] = "Construction", [0xE823] = "Recent clock time", [0xE825] = "Bank", [0xE826] = "DownloadMap",
        [0xE82D] = "Dictionary book", [0xE82F] = "ToolTip lightbulb idea", [0xE836] = "AddRemoteDevice", [0xE838] = "FolderOpen folder",
        [0xE839] = "Ethernet network", [0xE83D] = "DefenderApp", [0xE840] = "Pinned", [0xE850] = "Battery0", [0xE859] = "Battery9", [0xE865] = "VolumeBars",
        [0xE88E] = "USB", [0xE890] = "View eye", [0xE896] = "Download", [0xE897] = "Help question", [0xE898] = "Upload", [0xE899] = "Emoji",
        [0xE89A] = "TwoPage", [0xE89C] = "MailForward", [0xE8A0] = "OpenPane", [0xE8A3] = "ZoomIn", [0xE8A4] = "Bookmarks",
        [0xE8A5] = "Document file", [0xE8A6] = "ProtectedDocument", [0xE8A7] = "OpenInNewWindow", [0xE8A8] = "MailFill", [0xE8A9] = "ViewAll",
        [0xE8AA] = "VideoChat", [0xE8AB] = "Switch", [0xE8AC] = "Rename", [0xE8AD] = "Go", [0xE8AF] = "Remote", [0xE8B0] = "Click",
        [0xE8B1] = "Shuffle", [0xE8B2] = "Movies film", [0xE8B3] = "SelectAll", [0xE8B4] = "Orientation", [0xE8B5] = "Import", [0xE8B7] = "Folder",
        [0xE8B8] = "Webcam", [0xE8B9] = "Picture image", [0xE8BA] = "Caption", [0xE8BD] = "Message chat", [0xE8BE] = "Leaf nature",
        [0xE8BF] = "CalendarDay", [0xE8C0] = "CalendarWeek", [0xE8C1] = "Characters", [0xE8C3] = "Read", [0xE8C6] = "Cut scissors",
        [0xE8C7] = "PaymentCard credit card", [0xE8C8] = "Copy", [0xE8C9] = "Important", [0xE8CA] = "MailReply", [0xE8CB] = "Sort",
        [0xE8CC] = "MobileTablet", [0xE8CD] = "DisconnectDrive", [0xE8CE] = "MapDrive network drive", [0xE8D0] = "Priority", [0xE8D1] = "GotoToday",
        [0xE8D2] = "Font", [0xE8D3] = "FontColor", [0xE8D4] = "Contact2", [0xE8D5] = "FolderFill folder", [0xE8D6] = "Audio music note",
        [0xE8D7] = "Permissions key", [0xE8DA] = "OpenLocal", [0xE8DB] = "Italic", [0xE8DC] = "Underline", [0xE8DD] = "Bold",
        [0xE8DE] = "MoveToFolder", [0xE8E0] = "Dislike", [0xE8E1] = "Like thumbs up", [0xE8E5] = "OpenFile", [0xE8EA] = "CellPhone phone mobile",
        [0xE8EC] = "Tag label", [0xE8ED] = "RepeatOne", [0xE8EE] = "RepeatAll", [0xE8EF] = "Calculator", [0xE8F0] = "Directions",
        [0xE8F1] = "Library books", [0xE8F2] = "ChatBubbles chat", [0xE8F4] = "NewFolder", [0xE8F7] = "SyncFolder", [0xE8F8] = "BlockContact",
        [0xE8F9] = "SwitchApps", [0xE8FA] = "AddFriend", [0xE8FB] = "Accept", [0xE8FC] = "GoToStart", [0xE8FD] = "BulletedList list",
        [0xE8FE] = "Scan scanner", [0xE8FF] = "Preview", [0xE902] = "Group", [0xE909] = "World globe", [0xE90A] = "Comment", [0xE90B] = "MusicInfo",
        [0xE90F] = "Repair wrench tools", [0xE910] = "Accounts", [0xE912] = "Manage", [0xE913] = "Street", [0xE914] = "Printer3D",
        [0xE916] = "Stopwatch timer", [0xE91B] = "Photo picture", [0xE91C] = "ActionCenter", [0xE928] = "Fingerprint", [0xE929] = "Handwriting",
        [0xE930] = "Completed", [0xE932] = "Label", [0xE939] = "FeedbackApp", [0xE93C] = "MusicAlbum", [0xE93E] = "Streaming",
        [0xE943] = "Code developer", [0xE945] = "LightningBolt flash", [0xE946] = "Info information", [0xE950] = "Component", [0xE957] = "Sensor",
        [0xE958] = "StorageOptical disc cd dvd", [0xE95A] = "Communications", [0xE95B] = "Headset", [0xE95D] = "Projector", [0xE95E] = "Health heart",
        [0xE962] = "Mouse", [0xE963] = "Smartcard", [0xE965] = "MediaStorageTower", [0xE967] = "GameConsole", [0xE968] = "Network",
        [0xE977] = "PC1 computer", [0xE98F] = "ConstructionCone", [0xE990] = "XboxOneConsole xbox", [0xE99A] = "Robot", [0xE9D2] = "AreaChart chart",
        [0xE9D5] = "CheckList", [0xE9D9] = "Diagnostic", [0xE9E9] = "Equalizer", [0xE9F3] = "Process", [0xEA18] = "Shield security",
        [0xEA37] = "List", [0xEA86] = "Puzzle", [0xEB51] = "Heart love", [0xEB52] = "HeartFill love", [0xEB9F] = "GIF", [0xEBE8] = "Bug",
        [0xEC7A] = "DeveloperTools", [0xEC92] = "DateTime", [0xECA5] = "Tiles", [0xECAA] = "AppIconDefault app", [0xED15] = "Feedback",
        [0xED25] = "OpenFolderHorizontal", [0xEDA2] = "HardDrive drive disk", [0xEDE1] = "Export", [0xF0E2] = "GridView grid",
    };

    // German search words -> English name parts
    private static readonly Dictionary<string, string> German = new(StringComparer.OrdinalIgnoreCase)
    {
        ["haus"] = "home", ["start"] = "home", ["ordner"] = "folder", ["musik"] = "music", ["spiel"] = "game", ["spiele"] = "game",
        ["kamera"] = "camera", ["foto"] = "photo", ["bild"] = "picture", ["bilder"] = "picture", ["video"] = "video", ["film"] = "movies",
        ["einstellungen"] = "settings", ["zahnrad"] = "gear", ["post"] = "mail", ["brief"] = "mail", ["telefon"] = "phone", ["handy"] = "phone",
        ["suche"] = "search", ["lupe"] = "search", ["stern"] = "star", ["herz"] = "heart", ["schloss"] = "lock", ["schlüssel"] = "key",
        ["papierkorb"] = "trash", ["löschen"] = "delete", ["speichern"] = "save", ["drucker"] = "print", ["wolke"] = "cloud",
        ["kalender"] = "calendar", ["uhr"] = "clock", ["zeit"] = "time", ["weltkugel"] = "globe", ["internet"] = "globe", ["netzwerk"] = "network",
        ["laufwerk"] = "drive", ["festplatte"] = "drive", ["computer"] = "computer", ["bildschirm"] = "monitor", ["tastatur"] = "keyboard",
        ["maus"] = "mouse", ["kopfhörer"] = "headphone", ["lautsprecher"] = "speaker", ["mikrofon"] = "microphone",
        ["zug"] = "train", ["flugzeug"] = "airplane", ["buch"] = "book", ["bücher"] = "books", ["dokument"] = "document", ["datei"] = "file",
        ["werkzeug"] = "tools", ["schraubenschlüssel"] = "wrench", ["pinsel"] = "brush", ["farbe"] = "color", ["stift"] = "pen",
        ["einkaufen"] = "shop", ["warenkorb"] = "cart", ["geld"] = "bank", ["karte"] = "card", ["person"] = "person", ["benutzer"] = "user",
        ["gruppe"] = "group", ["leute"] = "people", ["chat"] = "chat", ["nachricht"] = "message", ["blatt"] = "leaf", ["natur"] = "nature",
        ["blitz"] = "lightning", ["sonne"] = "sun", ["mond"] = "moon", ["glühbirne"] = "lightbulb", ["idee"] = "idea", ["schild"] = "shield",
        ["sicherheit"] = "security", ["warnung"] = "warning", ["hilfe"] = "help", ["info"] = "info", ["herunterladen"] = "download",
        ["hochladen"] = "upload", ["teilen"] = "share", ["code"] = "code", ["terminal"] = "terminal", ["konsole"] = "console", ["roboter"] = "robot",
        ["käfer"] = "bug", ["puzzle"] = "puzzle", ["rechner"] = "calculator", ["taschenrechner"] = "calculator", ["diagramm"] = "chart",
        ["liste"] = "list", ["auge"] = "eye", ["ansicht"] = "view", ["paket"] = "package", ["box"] = "box", ["fahne"] = "flag", ["flagge"] = "flag",
        ["koffer"] = "briefcase", ["arbeit"] = "work", ["schule"] = "education", ["bildung"] = "education", ["bus"] = "bus", ["usb"] = "usb",
        ["cd"] = "disc", ["dvd"] = "disc", ["controller"] = "controller", ["xbox"] = "xbox", ["wlan"] = "wifi", ["strom"] = "power", ["power"] = "power",
        // Typical folder and drive names
        ["projekt"] = "work", ["dokument"] = "description", ["unterlagen"] = "description", ["foto"] = "photo", ["video"] = "movie",
        ["film"] = "movie", ["daten"] = "database", ["sicherung"] = "backup", ["privat"] = "lock", ["rechnung"] = "receipt", ["finanzen"] = "payments",
        ["steuer"] = "receipt", ["urlaub"] = "beach", ["reise"] = "flight", ["downloads"] = "download", ["vorlage"] = "content_copy", ["archiv"] = "archive",
        ["entwicklung"] = "code", ["kunde"] = "groups", ["kunden"] = "groups", ["familie"] = "family", ["rezept"] = "restaurant", ["auto"] = "car",
    };

    /// <summary>English search word for a German one ("haus" -> "home"), or null.</summary>
    public static string? Translate(string word)
    {
        if (German.TryGetValue(word, out var en)) return en;
        if (word.Length >= 4 && German.FirstOrDefault(kv => kv.Key.StartsWith(word, StringComparison.OrdinalIgnoreCase)).Value is { } start) return start;
        // Plural and compound forms: "projekte" -> projekt, "urlaubsfotos" stays unknown, "dokumente" -> dokument
        // Only real plural endings, so brand names don't turn into words ("Autodesk" is no car)
        foreach (var ending in new[] { "en", "er", "e", "n", "s" })
            if (word.Length > ending.Length + 3 && word.EndsWith(ending, StringComparison.OrdinalIgnoreCase) && German.TryGetValue(word[..^ending.Length], out var stem)) return stem;
        return null;
    }

    /// <summary>Extra search words of a Windows symbol (everything after its name).</summary>
    public static string TermsOf(string glyph) =>
        glyph.Length > 0 && Names.TryGetValue(char.ConvertToUtf32(glyph, 0), out var n) && n.IndexOf(' ') is int i and > 0 ? n[(i + 1)..] : "";

    public static string? NameOf(string glyph) =>
        glyph.Length > 0 && Names.TryGetValue(char.ConvertToUtf32(glyph, 0), out var n) ? n.Split(' ')[0] : null;

    /// <summary>Does the symbol match all search words (English names or German words)?</summary>
    public static bool Matches(string glyph, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        if (glyph.Length == 0 || !Names.TryGetValue(char.ConvertToUtf32(glyph, 0), out var name)) return false;
        string n = name.ToLowerInvariant();
        foreach (var word in query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            bool hit = n.Contains(word) || (German.TryGetValue(word, out var en) && n.Contains(en));
            if (!hit && word.Length >= 4) hit = German.Any(kv => kv.Key.StartsWith(word) && n.Contains(kv.Value));
            if (!hit) return false;
        }
        return true;
    }
}
