using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IconWorkshop.Controls;
using IconWorkshop.Imaging;
using IconWorkshop.Native;
using IconWorkshop.Packs;
using IconWorkshop.Programs;
using Microsoft.Win32;

namespace IconWorkshop.Views;

/// <summary>
/// Icon maker: tile (pack style or own shape, fill, border, shadow, gloss) plus any number of layers
/// (symbol, text, image, program icon, pack icon, badge) with color, outline, shadow and transform.
/// Designs are saved next to own icons so they can be edited again later.
/// </summary>
public partial class IconMakerPage : UserControl, IPage
{
    // ---------- Texts for XAML ----------
    public static string TitleText => Loc.T("Icon-Maker", "Icon maker");
    public static string SubtitleText => Loc.T("Baue eigene Icons im Stil eines Packs oder ganz frei: mit Ebenen, Farben, Verläufen und Effekten.",
                                               "Build your own icons in the style of a pack or completely free: with layers, colors, gradients and effects.");
    public static string TileTitle => Loc.T("Kachel", "Tile");
    public static string TileDesc => Loc.T("Form, Füllung und Stil eines Packs", "Shape, fill and the style of a pack");
    public static string PaletteText => Loc.T("Farbpalette", "Color palette");
    public static string SwatchTip => Loc.T("Als Hintergrund übernehmen", "Use as background");
    public static string GenSeriesText => Loc.T("Serie …", "Series …");
    public static string GenSeriesTip => Loc.T("Viele Icons im Stil der gewählten Variante, z. B. für alle Projektordner", "Many icons in the style of the selected variant, e.g. for all project folders");
    public static string GenTitle => Loc.T("1-Klick-Generator", "One-click generator");
    public static string GenDesc => Loc.T("Name eingeben oder Programm wählen, Variante anklicken, fertig", "Enter a name or choose a program, click a variant, done");
    public static string GenNameHintText => Loc.T("Wofür? z. B. Steam, Musik, Projekte, Backup", "For what? e.g. Steam, music, projects, backup");
    public static string GenProgramText => Loc.T("Programm …", "Program …");
    public static string GenProgramTip => Loc.T("Ein Programm wählen: sein Icon wird eine der Varianten und gibt die Farbe vor", "Choose a program: its icon becomes one of the variants and sets the color");
    public static string GenRollText => Loc.T("Würfeln", "Roll");
    public static string GenRollTip => Loc.T("Neue Varianten; angehakte Teile der gewählten Variante bleiben", "New variants; checked parts of the selected variant stay");
    public static string GenKeepText => Loc.T("Beim Würfeln behalten:", "Keep when rolling:");
    public static string GenLockContentText => Loc.T("Inhalt", "Content");
    public static string GenLockColorsText => Loc.T("Farben", "Colors");
    public static string GenLockShapeText => Loc.T("Form", "Shape");
    public static string GenLockLookText => Loc.T("Look", "Look");
    public static string TileEffectsTitle => Loc.T("Effekte der Kachel", "Tile effects");
    public static string TileEffectsDesc => Loc.T("Rand, Schatten, Glanz und Überlagerung", "Border, shadow, gloss and overlay");
    public static string LayersDesc => Loc.T("Symbole, Texte, Bilder und Badges übereinander", "Symbols, texts, images and badges stacked");
    public static string LayerPropsTitleText => Loc.T("Ausgewählte Ebene", "Selected layer");
    public static string LayerPropsDesc => Loc.T("Inhalt, Farbe, Größe, Position und Effekte", "Content, color, size, position and effects");
    public static string NewShortText => Loc.T("Neu", "New");
    public static string ExportText => Loc.T("Exportieren", "Export");
    public static string AssignShortText => Loc.T("Zuweisen", "Assign");
    public static string StyleSource => Loc.T("Stil von", "Style from");
    public static string ShapeText => Loc.T("Form", "Shape");
    public static string FillText => Loc.T("Füllung", "Fill");
    public static string AngleText => Loc.T("Winkel", "Angle");
    public static string EffectsText => Loc.T("Effekte", "Effects");
    public static string BorderText => Loc.T("Rand", "Border");
    public static string ShadowText => Loc.T("Schatten", "Shadow");
    public static string GlossText => Loc.T("Glanz", "Gloss");
    public static string OverlayText => Loc.T("Rahmen des Packs", "Pack frame");
    public static string ClipText => Loc.T("Ebenen auf die Form zuschneiden", "Cut layers to the shape");
    public static string BorderWidthText => Loc.T("Randbreite", "Border width");
    public static string ShadowSizeText => Loc.T("Schatten", "Shadow");
    public static string LayersTitle => Loc.T("Ebenen", "Layers");
    public static string UpText => Loc.T("Nach oben", "Move up");
    public static string DownText => Loc.T("Nach unten", "Move down");
    public static string DuplicateText => Loc.T("Duplizieren", "Duplicate");
    public static string VisibleText => Loc.T("Sichtbar", "Visible");
    public static string AddGlyphText => Loc.T("+ Symbol", "+ Symbol");
    public static string AddTextText => Loc.T("+ Text", "+ Text");
    public static string AddImageText => Loc.T("+ Bild", "+ Image");
    public static string AddProgramText => Loc.T("+ Programm-Icon", "+ Program icon");
    public static string AddPackIconText => Loc.T("+ Pack-Icon", "+ Pack icon");
    public static string AddBadgeText => Loc.T("+ Badge", "+ Badge");
    public static string NoLayersText => Loc.T("Keine Ebenen. Füge oben ein Symbol, einen Text oder ein Bild hinzu.", "No layers. Add a symbol, text or image above.");
    public static string SymbolText => Loc.T("Symbol", "Symbol");
    public static string GlyphSearchText => Loc.T("Symbole suchen, z. B. Haus, Musik, Spiel", "Search symbols, e.g. home, music, game");
    public static string TextText => Loc.T("Text", "Text");
    public static string BoldText => Loc.T("Fett", "Bold");
    public static string BadgeColorText => Loc.T("Badge-Farbe", "Badge color");
    public static string ImageText => Loc.T("Bild", "Image");
    public static string ChooseImageText => Loc.T("Datei …", "File …");
    public static string ChooseProgramText => Loc.T("Programm …", "Program …");
    public static string ChoosePackIconText => Loc.T("Pack-Icon …", "Pack icon …");
    public static string TintText => Loc.T("Einfarbig in der Füllfarbe", "One color in the fill color");
    public static string ColorText => Loc.T("Farbe", "Color");
    public static string OutlineText => Loc.T("Kontur", "Outline");
    public static string OutlineWidthText => Loc.T("Konturbreite", "Outline width");
    public static string SizeText => Loc.T("Größe", "Size");
    public static string RotationText => Loc.T("Drehung", "Rotation");
    public static string HorizontalText => Loc.T("Waagerecht", "Horizontal");
    public static string VerticalText => Loc.T("Senkrecht", "Vertical");
    public static string OpacityText => Loc.T("Deckkraft", "Opacity");
    public static string ShadowAngleText => Loc.T("Richtung", "Direction");
    public static string PreviewText => Loc.T("Vorschau", "Preview");
    public static string SaveText => Loc.T("In „Eigene Icons“ speichern", "Save to “My icons”");
    public static string SaveAsNewText => Loc.T("Als neues Icon speichern", "Save as new icon");
    public static string ExportPngText => Loc.T("Als PNG …", "As PNG …");
    public static string ExportIcoText => Loc.T("Als ICO …", "As ICO …");
    public static string NewDesignText => Loc.T("Neues Icon beginnen", "Start a new icon");

    private sealed record StyleOption(IconPack? Pack, string Label);
    private sealed record ShapeOption(TileShape Shape, string Label, Geometry? Geometry, string Glyph);
    private sealed record Choice<T>(T Value, string Label) { public override string ToString() => Label; }

    private static readonly string[] Fonts =
        ["Segoe UI Variable Display", "Segoe UI", "Bahnschrift", "Arial Black", "Consolas", "Georgia", "Impact", "Segoe Script", "Comic Sans MS"];

    private readonly MainWindow _main;
    private MakerDesign _d = MakerDesign.Default();
    private PackStyle _style = PackStyle.Neutral();
    private MakerLayer? _layer;
    // True until the page is built: controls raise change events while XAML is loaded (e.g. sliders when their minimum applies)
    private bool _loading = true;
    private IconFontSet _glyphSet = IconFonts.All[0];
    private List<ProgramEntry>? _programs;
    private IconPack? _editPack;
    private PackIcon? _editIcon;
    private IconItem? _lastSaved;
    private bool _nameAuto = true;
    private readonly List<(MakerDesign.Template Template, Image Image)> _templatePreviews = new();
    private readonly System.Windows.Threading.DispatcherTimer _templateTimer;

    public IconMakerPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _loading = true;
        // Template gallery: each button shows the current icon in that look
        foreach (var t in MakerDesign.Templates())
        {
            var img = new Image { Width = 48, Height = 48 };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            var content = new StackPanel();
            content.Children.Add(img);
            content.Children.Add(new TextBlock { Text = t.Name, FontSize = 12, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            var b = new Button
            {
                Content = content, Width = 108, Padding = new Thickness(4, 10, 4, 8), Margin = new Thickness(0, 0, 6, 6),
                ToolTip = Loc.T($"Look „{t.Name}“ übernehmen (Inhalt bleibt)", $"Apply the “{t.Name}” look (content stays)"),
            };
            b.Click += (_, _) => { t.Apply(_d); PushToUi(); Redraw(); };
            TemplateBar.Children.Add(b);
            _templatePreviews.Add((t, img));
        }
        _templateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _templateTimer.Tick += (_, _) => { _templateTimer.Stop(); UpdateTemplatePreviews(); };

        var shapeRect = new Rect(0, 0, 100, 100);
        ShapeList.ItemsSource = new List<ShapeOption>
        {
            new(TileShape.PackPlate, Loc.T("Form des Packs", "Pack shape"), null, ""),
            new(TileShape.RoundedSquare, Loc.T("Abgerundetes Quadrat", "Rounded square"), DesignRenderer.ShapeGeometry(TileShape.RoundedSquare, shapeRect), ""),
            new(TileShape.Squircle, Loc.T("Squircle", "Squircle"), DesignRenderer.ShapeGeometry(TileShape.Squircle, shapeRect), ""),
            new(TileShape.Circle, Loc.T("Kreis", "Circle"), DesignRenderer.ShapeGeometry(TileShape.Circle, shapeRect), ""),
            new(TileShape.Square, Loc.T("Quadrat", "Square"), DesignRenderer.ShapeGeometry(TileShape.Square, shapeRect), ""),
            new(TileShape.Portrait, Loc.T("Hochformat", "Portrait"), DesignRenderer.ShapeGeometry(TileShape.Portrait, shapeRect), ""),
            new(TileShape.Landscape, Loc.T("Querformat", "Landscape"), DesignRenderer.ShapeGeometry(TileShape.Landscape, shapeRect), ""),
            new(TileShape.Pill, Loc.T("Pille", "Pill"), DesignRenderer.ShapeGeometry(TileShape.Pill, shapeRect), ""),
            new(TileShape.Hexagon, Loc.T("Sechseck", "Hexagon"), DesignRenderer.ShapeGeometry(TileShape.Hexagon, shapeRect), ""),
            new(TileShape.Octagon, Loc.T("Achteck", "Octagon"), DesignRenderer.ShapeGeometry(TileShape.Octagon, shapeRect), ""),
            new(TileShape.Triangle, Loc.T("Dreieck", "Triangle"), DesignRenderer.ShapeGeometry(TileShape.Triangle, shapeRect), ""),
            new(TileShape.Shield, Loc.T("Schild", "Shield"), DesignRenderer.ShapeGeometry(TileShape.Shield, shapeRect), ""),
            new(TileShape.Drop, Loc.T("Tropfen", "Drop"), DesignRenderer.ShapeGeometry(TileShape.Drop, shapeRect), ""),
            new(TileShape.None, Loc.T("Keine Kachel", "No tile"), null, ""),
        };
        LayerFillKind.ItemsSource = new[] { new Choice<FillKind>(FillKind.Solid, Loc.T("Farbe", "Color")), new Choice<FillKind>(FillKind.Linear, Loc.T("Verlauf", "Gradient")), new Choice<FillKind>(FillKind.Radial, Loc.T("Radialer Verlauf", "Radial gradient")) };
        ShadowKindBox.ItemsSource = new[] { new Choice<ShadowKind>(ShadowKind.None, Loc.T("Kein Schatten", "No shadow")), new Choice<ShadowKind>(ShadowKind.Soft, Loc.T("Weicher Schatten", "Soft shadow")), new Choice<ShadowKind>(ShadowKind.Long, Loc.T("Langer Schatten", "Long shadow")) };
        FontBox.ItemsSource = Fonts;
        PaletteFamilyBox.ItemsSource = PaletteFamilies.All;
        PaletteFamilyBox.SelectedIndex = 0;
        GenCategoryList.ItemsSource = IconCategories.All;
        GenCategoryList.SelectedIndex = 0;
        GlyphSetBox.ItemsSource = IconFonts.All;
        GlyphSetBox.SelectedIndex = 0;
        ApplyGlyphSet(IconFonts.All[0]);
        main.Packs.Changed += () => { if (IsLoaded) FillStyles(); };
        _loading = false;
        FillStyles();
        StartNew();
    }

    public async void OnShown()
    {
        FillStyles();
        if (_programs is null)
        {
            try { _programs = (await ProgramScanner.ScanAsync()).Where(p => !string.IsNullOrEmpty(p.IconSource)).ToList(); }
            catch (Exception ex) { App.LogError(ex); _programs = new(); }
        }
    }

    // ===================================================================== Design life cycle

    private void StartNew()
    {
        _editPack = null;
        _editIcon = null;
        _lastSaved = null;
        _d = MakerDesign.Default();
        _d.StylePackId = (StyleBox.SelectedItem as StyleOption)?.Pack?.Id;
        _d.Layers[0].Fill = FillSpec.Of(ContrastColor());
        _d.Layers[0].Size = FitSize(0.55);
        _nameAuto = true;
        NameBox.Text = "";
        SavedText.Visibility = Visibility.Collapsed;
        AssignButton.IsEnabled = false;
        UpdateEditMode();
        _layer = _d.Layers.FirstOrDefault();
        PushToUi();
        Redraw();
    }

    /// <summary>Opens an icon from the library: own icons with their design for editing, other icons as an image layer of a new design.</summary>
    public void Open(IconItem item)
    {
        StartNew();
        string? designFile = PackStore.DesignPath(item.Pack, item.Icon);
        var design = designFile is null ? null : MakerDesign.FromJson(File.ReadAllText(designFile));
        if (design is not null && item.Pack.Id == PackStore.CustomPackId)
        {
            _d = design;
            _editPack = item.Pack;
            _editIcon = item.Icon;
            _lastSaved = item;
            AssignButton.IsEnabled = true;
            NameBox.Text = item.Title;
            _nameAuto = false;
            var styleOption = (StyleBox.ItemsSource as IEnumerable<StyleOption>)?.FirstOrDefault(o => o.Pack?.Id == _d.StylePackId);
            if (styleOption is not null) { _loading = true; StyleBox.SelectedItem = styleOption; _loading = false; }
            _style = PackStyle.For(styleOption?.Pack);
        }
        else
        {
            // Any icon as a layer: e.g. recolor it or put it into another style
            _d.Layers.Clear();
            var layer = new MakerLayer { Kind = LayerKind.Image, Size = 0.75, Fill = FillSpec.Of(ContrastColor()), Name = item.Title };
            layer.SetImage(File.ReadAllBytes(item.Path));
            _d.Layers.Add(layer);
            _d.Shape = TileShape.None;
            NameBox.Text = item.Title;
        }
        UpdateEditMode();
        _layer = _d.Layers.FirstOrDefault();
        PushToUi();
        Redraw();
    }

    private void UpdateEditMode()
    {
        bool editing = _editIcon is not null;
        PageTitleText.Text = editing ? Loc.T($"Icon-Maker · „{_editIcon!.Title}“ bearbeiten", $"Icon maker · edit “{_editIcon!.Title}”") : TitleText;
        SaveButtonText.Text = editing ? Loc.T("Änderungen speichern", "Save changes") : SaveText;
        SaveAsNewButton.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void New_Click(object sender, RoutedEventArgs e) => StartNew();

    // ===================================================================== Style source

    private void FillStyles()
    {
        var previous = (StyleBox.SelectedItem as StyleOption)?.Pack?.Id ?? _d.StylePackId;
        var options = _main.Packs.Packs.Where(p => p.Id != PackStore.CustomPackId).Select(p => new StyleOption(p, p.Name)).ToList();
        options.Add(new StyleOption(null, Loc.T("Neutral (ohne Pack)", "Neutral (no pack)")));
        _loading = true;
        StyleBox.ItemsSource = options;
        StyleBox.SelectedItem = (previous is null ? null : options.FirstOrDefault(o => o.Pack?.Id == previous)) ?? options[0];
        _loading = false;
        ApplyStyleSource();
    }

    private void StyleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        ApplyStyleSource();
        PushToUi();
        Redraw();
        if (GenList.ItemsSource is not null) RollGenerator(newSeed: false);
    }

    private void ApplyStyleSource()
    {
        var option = StyleBox.SelectedItem as StyleOption;
        _style = PackStyle.For(option?.Pack);
        _d.StylePackId = option?.Pack?.Id;
        StyleHint.Text = option?.Pack is null
            ? Loc.T("Ohne Pack: wähle eine Form und Füllung.", "Without a pack: choose a shape and fill.")
            : _style.IsNeutral
                ? Loc.T("Dieses Pack bringt keine eigene Kachel mit, und seine Icons haben keine einheitliche Form (freie Formen). „Form des Packs“ ist dann eine neutrale Kachel; wähle sonst eine Form.",
                        "This pack has no plate of its own and its icons have no uniform shape (free-form). “Pack shape” is a neutral tile then; or choose a shape.")
                : _style.IsLearned
                    ? Loc.T("Dieses Pack bringt keinen eigenen Stil mit. „Form des Packs“ ist die gemeinsame Form seiner Icons, aus dem Pack abgeleitet.",
                            "This pack has no style of its own. “Pack shape” is the common shape of its icons, derived from the pack.")
                    : Loc.T("„Form des Packs“ nutzt Kachel, Maske und Rahmen des Packs, wie Android-Launcher für fremde Apps.",
                            "“Pack shape” uses the pack's plate, mask and frame, like Android launchers do for other apps.");
    }

    // ===================================================================== Model <-> UI

    /// <summary>Shows the design and the selected layer in the controls.</summary>
    private void PushToUi()
    {
        _loading = true;
        try
        {
            ShapeList.SelectedItem = (ShapeList.ItemsSource as IEnumerable<ShapeOption>)?.FirstOrDefault(s => s.Shape == _d.Shape);
            var bgChoices = new List<Choice<(bool Plate, FillKind Kind)>>();
            if (_d.Shape == TileShape.PackPlate && _style.Back is not null) bgChoices.Add(new((true, FillKind.Solid), Loc.T("Kachel des Packs", "Pack plate")));
            bgChoices.Add(new((false, FillKind.Solid), Loc.T("Farbe", "Color")));
            bgChoices.Add(new((false, FillKind.Linear), Loc.T("Verlauf", "Gradient")));
            bgChoices.Add(new((false, FillKind.Radial), Loc.T("Radialer Verlauf", "Radial gradient")));
            BgKind.ItemsSource = bgChoices;
            bool plate = _d.UsePackPlate && bgChoices[0].Value.Plate;
            BgKind.SelectedItem = bgChoices.FirstOrDefault(c => c.Value.Plate == plate && (plate || c.Value.Kind == _d.Background.Kind)) ?? bgChoices[0];
            BgColor1.Color = _d.Background.C1;
            BgColor2.Color = _d.Background.C2;
            BgAngle.Value = _d.Background.Angle;
            BorderOn.IsChecked = _d.Border.On;
            BorderColor.Color = _d.Border.C;
            BorderWidth.Value = _d.Border.Width;
            TileShadowOn.IsChecked = _d.TileShadow.Kind != ShadowKind.None;
            TileShadowColor.Color = _d.TileShadow.C;
            TileShadowSize.Value = _d.TileShadow.Size;
            GlossOn.IsChecked = _d.Gloss;
            OverlayOn.IsChecked = _d.PackOverlay;
            ClipOn.IsChecked = _d.ClipToShape;
            UpdateTileVisibility();

            LayerList.ItemsSource = null;
            LayerList.ItemsSource = _d.Layers.AsEnumerable().Reverse().ToList(); // top layer first, like in image editors
            LayerList.SelectedItem = _layer;
            NoLayersHint.Visibility = _d.Layers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LayerProps.Visibility = _layer is null ? Visibility.Collapsed : Visibility.Visible;
            LayerPropsTitle.Text = _layer is null ? LayerPropsTitleText : $"{LayerPropsTitleText}: {_layer.DisplayName}";

            if (_layer is { } l)
            {
                GlyphPanel.Visibility = l.Kind == LayerKind.Glyph ? Visibility.Visible : Visibility.Collapsed;
                TextPanel.Visibility = l.Kind is LayerKind.Text or LayerKind.Badge ? Visibility.Visible : Visibility.Collapsed;
                BadgePanel.Visibility = l.Kind == LayerKind.Badge ? Visibility.Visible : Visibility.Collapsed;
                ImagePanel.Visibility = l.Kind == LayerKind.Image ? Visibility.Visible : Visibility.Collapsed;
                bool fillVisible = l.Kind != LayerKind.Image || l.Tint;
                LayerFillLabel.Visibility = LayerFillPanel.Visibility = fillVisible ? Visibility.Visible : Visibility.Collapsed;
                LayerFillLabel.Text = l.Kind == LayerKind.Badge ? Loc.T("Textfarbe", "Text color") : ColorText;
                var layerSet = IconFonts.Get(l.GlyphSet);
                if (layerSet != _glyphSet) { GlyphSetBox.SelectedItem = layerSet; ApplyGlyphSet(layerSet); }
                GlyphList.Select(l.Glyph);
                GlyphInfo.Text = GlyphInfoText(l.Glyph);
                LayerText.Text = l.Text;
                FontBox.SelectedItem = Fonts.Contains(l.Font) ? l.Font : Fonts[0];
                BoldBox.IsChecked = l.Bold;
                BadgeColor.Color = l.BadgeFill.C1;
                TintBox.IsChecked = l.Tint;
                LayerFillKind.SelectedItem = ((IEnumerable<Choice<FillKind>>)LayerFillKind.ItemsSource).First(c => c.Value == l.Fill.Kind);
                LayerColor1.Color = l.Fill.C1;
                LayerColor2.Color = l.Fill.C2;
                LayerColor2.Visibility = l.Fill.Kind == FillKind.Solid ? Visibility.Collapsed : Visibility.Visible;
                OutlineOn.IsChecked = l.Outline.On;
                OutlineColor.Color = l.Outline.C;
                OutlineWidth.Value = l.Outline.Width;
                ShadowKindBox.SelectedItem = ((IEnumerable<Choice<ShadowKind>>)ShadowKindBox.ItemsSource).First(c => c.Value == l.Shadow.Kind);
                ShadowColor.Color = l.Shadow.C;
                ShadowSize.Value = l.Shadow.Size;
                ShadowAngle.Value = l.Shadow.Angle;
                LayerSize.Value = l.Size * 100;
                LayerRotation.Value = l.Rotation;
                LayerX.Value = l.X * 100;
                LayerY.Value = l.Y * 100;
                LayerOpacity.Value = l.Opacity * 100;
            }
        }
        finally { _loading = false; }
    }

    private void UpdateTileVisibility()
    {
        bool tile = _d.Shape != TileShape.None;
        bool plate = BgKind.SelectedItem is Choice<(bool Plate, FillKind Kind)> c && c.Value.Plate;
        BgKind.IsEnabled = tile;
        BgColor1.Visibility = tile && !plate ? Visibility.Visible : Visibility.Collapsed;
        BgColor2.Visibility = tile && !plate && _d.Background.Kind != FillKind.Solid ? Visibility.Visible : Visibility.Collapsed;
        BgAnglePanel.Visibility = tile && !plate && _d.Background.Kind == FillKind.Linear ? Visibility.Visible : Visibility.Collapsed;
        OverlayOn.Visibility = _d.Shape == TileShape.PackPlate && _style.Upon is not null ? Visibility.Visible : Visibility.Collapsed;
        BorderOn.IsEnabled = BorderColor.IsEnabled = tile && _d.Shape != TileShape.PackPlate;
    }

    /// <summary>Reads all controls into the design and the selected layer, then redraws.</summary>
    private void ReadFromUi()
    {
        if (_loading) return;
        if (BgKind.SelectedItem is Choice<(bool Plate, FillKind Kind)> bg)
        {
            _d.UsePackPlate = bg.Value.Plate;
            if (!bg.Value.Plate) _d.Background.Kind = bg.Value.Kind;
        }
        _d.Background.C1 = BgColor1.Color;
        _d.Background.C2 = BgColor2.Color;
        _d.Background.Angle = BgAngle.Value;
        _d.Border.On = BorderOn.IsChecked == true;
        _d.Border.C = BorderColor.Color;
        _d.Border.Width = BorderWidth.Value;
        _d.TileShadow.Kind = TileShadowOn.IsChecked == true ? ShadowKind.Soft : ShadowKind.None;
        _d.TileShadow.C = TileShadowColor.Color;
        _d.TileShadow.Size = TileShadowSize.Value;
        _d.Gloss = GlossOn.IsChecked == true;
        _d.PackOverlay = OverlayOn.IsChecked == true;
        _d.ClipToShape = ClipOn.IsChecked == true;

        if (_layer is { } l)
        {
            l.Text = LayerText.Text;
            if (FontBox.SelectedItem is string f) l.Font = f;
            l.Bold = BoldBox.IsChecked == true;
            l.BadgeFill.C1 = BadgeColor.Color;
            l.Tint = TintBox.IsChecked == true;
            if (LayerFillKind.SelectedItem is Choice<FillKind> fk) l.Fill.Kind = fk.Value;
            l.Fill.C1 = LayerColor1.Color;
            l.Fill.C2 = LayerColor2.Color;
            l.Outline.On = OutlineOn.IsChecked == true;
            l.Outline.C = OutlineColor.Color;
            l.Outline.Width = OutlineWidth.Value;
            if (ShadowKindBox.SelectedItem is Choice<ShadowKind> sk)
            {
                // Long shadows need more length than soft ones need blur: sensible start values when switching
                if (sk.Value != l.Shadow.Kind && sk.Value == ShadowKind.Long && l.Shadow.Size < 60) l.Shadow.Size = 120;
                if (sk.Value != l.Shadow.Kind && sk.Value == ShadowKind.Soft && l.Shadow.Size > 40) l.Shadow.Size = 14;
                l.Shadow.Kind = sk.Value;
            }
            l.Shadow.C = ShadowColor.Color;
            l.Shadow.Size = ShadowSize.Value;
            l.Shadow.Angle = ShadowAngle.Value;
            l.Size = LayerSize.Value / 100;
            l.Rotation = LayerRotation.Value;
            l.X = LayerX.Value / 100;
            l.Y = LayerY.Value / 100;
            l.Opacity = LayerOpacity.Value / 100;
        }
        Redraw();
    }

    private void Changed_Click(object sender, RoutedEventArgs e) { ReadFromUi(); PushToUi(); }
    private void Changed_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) { ReadFromUi(); PushToUi(); } }
    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => ReadFromUi();
    private void Color_Changed(Color c) => ReadFromUi();

    private void ShapeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ShapeList.SelectedItem is not ShapeOption s) return;
        // Content that sat in the old shape's content area moves into the new one (folder: onto the front panel)
        var (oldY, _) = DesignRenderer.ContentArea(_d.Shape);
        var (newY, maxSize) = DesignRenderer.ContentArea(s.Shape);
        foreach (var l in _d.Layers.Where(l => l.Kind != LayerKind.Badge && Math.Abs(l.Y - oldY) < 0.005))
        {
            l.Y = newY;
            l.Size = Math.Min(l.Size, maxSize);
        }
        _d.Shape = s.Shape;
        if (s.Shape != TileShape.PackPlate) _d.UsePackPlate = false;
        PushToUi();
        Redraw();
    }

    // ===================================================================== Layers

    private void LayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LayerList.SelectedItem is not MakerLayer l) return;
        _layer = l;
        PushToUi();
    }

    private void LayerVisible_Click(object sender, RoutedEventArgs e) => Redraw();

    private void AddLayer(MakerLayer layer)
    {
        if (layer.Kind != LayerKind.Badge && layer.Y == 0) layer.Y = DesignRenderer.ContentArea(_d.Shape).Y;
        _d.Layers.Add(layer);
        _layer = layer;
        PushToUi();
        Redraw();
    }

    /// <summary>Default layer size that fits the tile (narrow pack plates get smaller content).</summary>
    private double FitSize(double wanted) =>
        _d.Shape == TileShape.PackPlate ? Math.Round(Math.Min(wanted, _style.ShapeWidth * 0.78), 2) : _d.Shape == TileShape.Portrait ? Math.Min(wanted, 0.5) : Math.Min(wanted, DesignRenderer.ContentArea(_d.Shape).MaxSize);

    private Color ContrastColor()
    {
        if (_d.Shape == TileShape.None) return Color.FromRgb(0x20, 0x20, 0x24);
        if (_d.Shape == TileShape.PackPlate && _d.UsePackPlate && _style.Back is not null) return _style.ContrastColor;
        var bg = _d.Background.C1;
        return 0.2126 * bg.R + 0.7152 * bg.G + 0.0722 * bg.B > 150 ? Color.FromRgb(0x20, 0x20, 0x24) : Colors.White;
    }

    private void AddGlyph_Click(object sender, RoutedEventArgs e) =>
        AddLayer(new MakerLayer { Kind = LayerKind.Glyph, Glyph = "", Fill = FillSpec.Of(ContrastColor()), Size = FitSize(0.55) });

    private void AddText_Click(object sender, RoutedEventArgs e) =>
        AddLayer(new MakerLayer { Kind = LayerKind.Text, Text = "Aa", Fill = FillSpec.Of(ContrastColor()), Size = FitSize(0.6) });

    private void AddBadge_Click(object sender, RoutedEventArgs e) =>
        AddLayer(new MakerLayer { Kind = LayerKind.Badge, Text = "1", Fill = FillSpec.Of(Colors.White), Size = 0.3, X = 0.3, Y = -0.3 });

    private void AddImage_Click(object sender, RoutedEventArgs e)
    {
        if (PickImageFile() is { } png) AddLayer(ImageLayer(png, null));
    }

    private void AddProgram_Click(object sender, RoutedEventArgs e)
    {
        if (PickProgram() is { } r) AddLayer(ImageLayer(r.Png, r.Name));
    }

    private void AddPackIcon_Click(object sender, RoutedEventArgs e)
    {
        if (PackIconDialog.Pick(_main, _main.Packs, Loc.T("Pack-Icon als Ebene", "Pack icon as layer")) is { } item)
            AddLayer(ImageLayer(File.ReadAllBytes(item.Path), item.Title));
    }

    private MakerLayer ImageLayer(byte[] png, string? name)
    {
        var l = new MakerLayer { Kind = LayerKind.Image, Size = FitSize(0.7), Fill = FillSpec.Of(ContrastColor()), Name = name ?? "" };
        l.SetImage(png);
        if (_nameAuto && name is not null) NameBox.Text = name;
        return l;
    }

    private void ReplaceImage_Click(object sender, RoutedEventArgs e)
    {
        if (_layer is null || PickImageFile() is not { } png) return;
        _layer.SetImage(png);
        _layer.Name = "";
        PushToUi(); Redraw();
    }

    private void ReplaceProgram_Click(object sender, RoutedEventArgs e)
    {
        if (_layer is null || PickProgram() is not { } r) return;
        _layer.SetImage(r.Png);
        _layer.Name = r.Name;
        PushToUi(); Redraw();
    }

    private void ReplacePackIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_layer is null || PackIconDialog.Pick(_main, _main.Packs, Loc.T("Pack-Icon wählen", "Choose pack icon")) is not { } item) return;
        _layer.SetImage(File.ReadAllBytes(item.Path));
        _layer.Name = item.Title;
        PushToUi(); Redraw();
    }

    private void LayerUp_Click(object sender, RoutedEventArgs e) => MoveLayer(+1);
    private void LayerDown_Click(object sender, RoutedEventArgs e) => MoveLayer(-1);

    /// <summary>+1 = towards the top (drawn later).</summary>
    private void MoveLayer(int delta)
    {
        if (_layer is null) return;
        int i = _d.Layers.IndexOf(_layer), j = i + delta;
        if (i < 0 || j < 0 || j >= _d.Layers.Count) return;
        (_d.Layers[i], _d.Layers[j]) = (_d.Layers[j], _d.Layers[i]);
        PushToUi(); Redraw();
    }

    private void LayerDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_layer is null) return;
        var copy = _layer.Clone();
        copy.X = Math.Min(0.5, copy.X + 0.05);
        copy.Y = Math.Min(0.5, copy.Y + 0.05);
        _d.Layers.Insert(_d.Layers.IndexOf(_layer) + 1, copy);
        _layer = copy;
        PushToUi(); Redraw();
    }

    private void LayerDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_layer is null) return;
        int i = _d.Layers.IndexOf(_layer);
        _d.Layers.Remove(_layer);
        _layer = _d.Layers.Count == 0 ? null : _d.Layers[Math.Clamp(i - 1, 0, _d.Layers.Count - 1)];
        PushToUi(); Redraw();
    }

    // ---------- Symbol search and text ----------

    private void GlyphSetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GlyphSetBox.SelectedItem is IconFontSet set && set != _glyphSet) ApplyGlyphSet(set);
    }

    /// <summary>Shows the symbols of another icon font (the layer only switches when a symbol is chosen).</summary>
    private void ApplyGlyphSet(IconFontSet set)
    {
        _glyphSet = set;
        GlyphList.GlyphFont = set.Family;
        GlyphList.GlyphWeight = set.Weight;
        GlyphList.NameOf = set.NameOf;
        GlyphList.SetGlyphs(set.Search(GlyphSearch.Text.Trim()));
        GlyphList.Select(_layer?.GlyphSet == set.Id ? _layer.Glyph : null);
        GlyphInfo.Text = set.Attribution ?? "";
    }

    private string GlyphInfoText(string glyph)
    {
        string name = _glyphSet.NameOf(glyph) ?? $"U+{char.ConvertToUtf32(glyph, 0):X4}";
        return _glyphSet.Attribution is { } a ? $"{name}  00b7  {a}" : name;
    }

    private void GlyphSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        string q = GlyphSearch.Text.Trim();
        GlyphSearchHint.Visibility = q.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        GlyphList.SetGlyphs(_glyphSet.Search(q));
    }

    private void GlyphList_GlyphChosen(string g)
    {
        if (_loading || _layer is null) return;
        _layer.Glyph = g;
        _layer.GlyphSet = _glyphSet.Id;
        GlyphInfo.Text = GlyphInfoText(g);
        if (_nameAuto && _glyphSet.NameOf(g) is { } n) NameBox.Text = n;
        RefreshLayerNames();
        Redraw();
    }

    private void LayerText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        ReadFromUi();
        if (_nameAuto && _layer?.Kind == LayerKind.Text) NameBox.Text = LayerText.Text;
        RefreshLayerNames();
    }

    private void RefreshLayerNames()
    {
        _loading = true;
        var selected = _layer;
        LayerList.ItemsSource = null;
        LayerList.ItemsSource = _d.Layers.AsEnumerable().Reverse().ToList();
        LayerList.SelectedItem = selected;
        _loading = false;
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (NameBox.IsKeyboardFocused) _nameAuto = false;
    }

    // ===================================================================== Drawing

    private BitmapSource Render() => DesignRenderer.Render(_d, _style);

    private void Redraw()
    {
        if (_loading) return;
        var img = Render();
        Preview.Source = img;
        SmallLight48.Source = SmallDark48.Source = IcoWriter.Render(img, 48);
        SmallLight32.Source = SmallDark32.Source = IcoWriter.Render(img, 32);
        SmallLight16.Source = SmallDark16.Source = IcoWriter.Render(img, 16);
        _templateTimer.Stop();
        _templateTimer.Start();
    }

    // ===================================================================== Pickers

    private byte[]? PickImageFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = Loc.T("Bild wählen", "Choose image"),
            Filter = Loc.T("Bilder (*.png;*.ico;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.webp;*.bmp",
                           "Images (*.png;*.ico;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.webp;*.bmp"),
        };
        if (dlg.ShowDialog(_main) != true) return null;
        try
        {
            if (_nameAuto) NameBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            return ImageTools.ToPng(File.ReadAllBytes(dlg.FileName)); // ICO: largest frame
        }
        catch (Exception ex)
        {
            FluentDialog.Show(_main, Loc.T("Bild nicht lesbar", "Image not readable"), ex.Message, Loc.Ok, glyph: FluentDialog.GlyphError);
            return null;
        }
    }

    private (byte[] Png, string Name)? PickProgram()
    {
        var programs = _programs ?? new();
        var picked = ProgramPickerDialog.Pick(_main, programs);
        if (picked is null) return null;
        if (ProgramPng(picked) is not { } png)
        {
            FluentDialog.Show(_main, Loc.T("Kein Icon", "No icon"), Loc.T("Für dieses Programm konnte kein Icon gelesen werden.", "No icon could be read for this program."), Loc.Ok);
            return null;
        }
        return (png, picked.Name);
    }

    /// <summary>The current icon of a program as a 256 px PNG.</summary>
    public static byte[]? ProgramPng(ProgramEntry program)
    {
        var img = ShellNative.GetItemImage(program.IconSource, 256);
        if (img is null) return null;
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(img));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    // ===================================================================== One-click generator

    private sealed class GenItem(GenParts parts, MakerDesign design, ImageSource image)
    {
        public GenParts Parts { get; } = parts;
        public MakerDesign Design { get; } = design;
        public ImageSource Image { get; } = image;
    }

    private const int GenCount = 18;
    private byte[]? _genProgramPng;
    private int _genSeed;
    private System.Windows.Threading.DispatcherTimer? _genDelay;

    /// <summary>Opens the generator for a program (e.g. from the program list): name and icon prefilled, variants shown.</summary>
    public void GenerateFor(ProgramEntry program)
    {
        StartNew();
        GenSection.IsExpanded = true;
        // A blank icon (missing icon file, typical for Steam) is no content; Steam's tiny icon still gives the colors
        SetGenProgram(program.Name, program.IconMissing ? null : ProgramPng(program));
        _genColorPng = ColorHint(program);
        _loadingGen = true;
        GenName.Text = program.Name;
        if (program.Game is not null) GenCategoryList.SelectedItem = IconCategories.Games;
        _loadingGen = false;
        RollGenerator(newSeed: false);
    }

    private byte[]? _genColorPng;

    /// <summary>Steam's small game icon when the shortcut's own icon is missing: too small as content, good for the colors.</summary>
    public static byte[]? ColorHint(ProgramEntry program)
    {
        if (!program.IconMissing || program.Game is not { Launcher: GameLauncher.Steam } game) return null;
        try { return GameLibrary.SteamClientIcon(game.Id) is { } file ? File.ReadAllBytes(file) : null; }
        catch { return null; }
    }

    private bool _loadingGen;

    /// <summary>Opens the generator for a drive, folder or file type: name and category prefilled, variants shown.</summary>
    public void GenerateFor(string name, IconCategory category)
    {
        StartNew();
        GenSection.IsExpanded = true;
        SetGenProgram(null, null);
        _loadingGen = true;
        GenName.Text = name;
        GenCategoryList.SelectedItem = category;
        _loadingGen = false;
        RollGenerator(newSeed: false);
    }

    private void GenSeries_Click(object sender, RoutedEventArgs e)
    {
        if (GenList.SelectedItem is not GenItem item) return;
        var input = GenInputNow();
        var results = SeriesDialog.Show(_main, item.Parts, input, _programs ?? new());
        if (results is null || results.Count == 0) return;
        try
        {
            foreach (var r in results)
                _main.Packs.SaveCustomIcon(r.Name, DesignRenderer.RenderPng(r.Design, _style), input.Category.Name, r.Design.ToJson());
            SavedText.Text = Loc.T($"{results.Count} Icons gespeichert. Du findest sie in der Bibliothek im Pack „Eigene Icons“ und kannst sie von dort zuweisen.",
                                   $"{results.Count} icons saved. You'll find them in the library in the “My icons” pack and can assign them from there.");
        }
        catch (Exception ex) { App.LogError(ex); SavedText.Text = ex.Message; }
        SavedText.Visibility = Visibility.Visible;
    }

    private void GenCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingGen && IsLoaded) RollGenerator(newSeed: false);
    }

    // ---------- Color palettes in the editor ----------

    private sealed record SwatchItem(GenPalette Palette)
    {
        public Brush Brush { get; } = new LinearGradientBrush(Palette.C1, Palette.C2, 45);
    }

    private void PaletteFamilyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PaletteFamilyBox.SelectedItem is PaletteFamily family) PaletteSwatches.ItemsSource = family.Palettes.Select(p => new SwatchItem(p)).ToList();
    }

    /// <summary>Sets the background to a palette; content that had the old contrast color gets the new one.</summary>
    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SwatchItem swatch) return;
        var oldInk = ContrastColor();
        _d.Background.C1 = swatch.Palette.C1;
        _d.Background.C2 = swatch.Palette.C2;
        if (_d.Shape == TileShape.PackPlate) _d.UsePackPlate = false;
        if (_d.Shape == TileShape.None) _d.Shape = TileShape.Squircle;
        var newInk = ContrastColor();
        foreach (var l in _d.Layers.Where(l => l.Kind != LayerKind.Badge && (l.Kind != LayerKind.Image || l.Tint) && l.Fill.C1 == oldInk))
            l.Fill.C1 = newInk;
        PushToUi();
        Redraw();
    }

    private void SetGenProgram(string? name, byte[]? png)
    {
        _genProgramPng = png;
        _genColorPng = null;
        GenProgramChip.Visibility = png is null ? Visibility.Collapsed : Visibility.Visible;
        GenProgramName.Text = name ?? "";
        GenProgramIcon.Source = png is null ? null : ImageTools.DecodeThumbnail(png, 36);
    }

    private void GenName_TextChanged(object sender, TextChangedEventArgs e)
    {
        GenNameHint.Visibility = GenName.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_loadingGen) return;
        _genDelay ??= CreateGenDelay();
        _genDelay.Stop();
        _genDelay.Start();
    }

    private System.Windows.Threading.DispatcherTimer CreateGenDelay()
    {
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        t.Tick += (_, _) => { t.Stop(); RollGenerator(newSeed: false); };
        return t;
    }

    private void GenProgram_Click(object sender, RoutedEventArgs e)
    {
        var picked = ProgramPickerDialog.Pick(_main, _programs ?? new());
        if (picked is null) return;
        SetGenProgram(picked.Name, ProgramPng(picked));
        _loadingGen = true;
        GenName.Text = picked.Name;
        _loadingGen = false;
        RollGenerator(newSeed: false);
    }

    private void GenProgramClear_Click(object sender, RoutedEventArgs e)
    {
        SetGenProgram(null, null);
        RollGenerator(newSeed: false);
    }

    private void GenRoll_Click(object sender, RoutedEventArgs e) => RollGenerator(newSeed: true);

    private GenInput GenInputNow() => new()
    {
        Name = GenName.Text.Trim(),
        ProgramPng = _genProgramPng,
        ColorPng = _genColorPng,
        Style = _style,
        StylePackId = (StyleBox.SelectedItem as StyleOption)?.Pack?.Id,
        Category = GenCategoryList.SelectedItem as IconCategory ?? IconCategories.Free,
    };

    /// <summary>Fills the variant grid. A new seed gives new combinations; locked parts of the selected variant stay.</summary>
    private void RollGenerator(bool newSeed)
    {
        var input = GenInputNow();
        if (input.Name.Length == 0 && input.ProgramPng is null && input.Category == IconCategories.Free) { GenList.ItemsSource = null; UpdateGenLocks(); return; }
        _genSeed = newSeed ? Environment.TickCount & 0x7FFFFFFF : 0;
        var keep = (GenList.SelectedItem as GenItem)?.Parts;
        var locks = keep is null ? GenLocks.None
            : new GenLocks(GenLockContent.IsChecked == true, GenLockColors.IsChecked == true, GenLockShape.IsChecked == true, GenLockLook.IsChecked == true);
        var items = new List<GenItem>();
        foreach (var parts in IconGenerator.Roll(input, GenCount, _genSeed, locks, keep))
        {
            try
            {
                var design = IconGenerator.Compose(parts, input);
                items.Add(new GenItem(parts, design, IcoWriter.Render(DesignRenderer.Render(design, _style), 96)));
            }
            catch (Exception ex) { App.LogError(ex); }
        }
        // The kept variant stays first, so it doesn't vanish while rolling
        if (keep is not null && GenList.SelectedItem is GenItem selected) items.Insert(0, selected);
        _loadingGen = true;
        GenList.ItemsSource = items;
        GenList.SelectedItem = keep is not null ? items[0] : null;
        _loadingGen = false;
        UpdateGenLocks();
    }

    private void UpdateGenLocks()
    {
        bool any = GenList.SelectedItem is GenItem;
        GenSeriesButton.IsEnabled = any;
        foreach (var cb in new[] { GenLockContent, GenLockColors, GenLockShape, GenLockLook })
        {
            cb.IsEnabled = any;
            if (!any) cb.IsChecked = false;
        }
    }

    /// <summary>A variant becomes the current design (a new icon), ready to save, assign or fine-tune below.</summary>
    private void GenList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateGenLocks();
        if (_loadingGen || GenList.SelectedItem is not GenItem item) return;
        _editPack = null;
        _editIcon = null;
        _lastSaved = null;
        AssignButton.IsEnabled = false;
        SavedText.Visibility = Visibility.Collapsed;
        _d = item.Design.Clone();
        _layer = _d.Layers.FirstOrDefault();
        if (_nameAuto) NameBox.Text = GenName.Text.Trim();
        UpdateEditMode();
        PushToUi();
        Redraw();
    }

    // ===================================================================== Save, assign, export

    private string IconName() => string.IsNullOrWhiteSpace(NameBox.Text) ? Loc.T("eigenes icon", "my icon") : NameBox.Text;

    private void Save_Click(object sender, RoutedEventArgs e) => Save(asNew: false);
    private void SaveAsNew_Click(object sender, RoutedEventArgs e) => Save(asNew: true);

    private void Save(bool asNew)
    {
        try
        {
            byte[] png = DesignRenderer.RenderPng(_d, _style);
            string json = _d.ToJson();
            if (!asNew && _editPack is not null && _editIcon is not null)
            {
                _main.Packs.UpdateCustomIcon(_editPack, _editIcon, png, json);
                // Assigned copies of this icon get the new look
                ShellNative.NotifyIconsChanged();
                var shared = IconItem.For(_editPack).FirstOrDefault(i => i.Icon == _editIcon);
                shared?.SetThumb(null);
                _lastSaved = shared;
                SavedText.Text = Loc.T($"„{_editIcon.Title}“ aktualisiert. Wo es zugewiesen ist, erscheint das neue Aussehen (ggf. nach „Icons aktualisieren“).",
                                       $"“{_editIcon.Title}” updated. Where it is assigned, the new look appears (possibly after “Refresh icons”).");
            }
            else
            {
                var (pack, icon) = _main.Packs.SaveCustomIcon(IconName(), png, (StyleBox.SelectedItem as StyleOption)?.Label ?? "", json);
                _editPack = pack;
                _editIcon = icon;
                _lastSaved = IconItem.For(pack).FirstOrDefault(i => i.Icon == icon);
                SavedText.Text = Loc.T($"Gespeichert als „{icon.Title}“ in {pack.Name}. Du findest es auch in der Bibliothek.",
                                       $"Saved as “{icon.Title}” in {pack.Name}. You'll also find it in the library.");
            }
            UpdateEditMode();
            AssignButton.IsEnabled = _lastSaved is not null;
            SavedText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            SavedText.Text = ex.Message;
            SavedText.Visibility = Visibility.Visible;
        }
    }

    private void Assign_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSaved is not { } item) return;
        AssignMenu.Items.Clear();
        foreach (var (index, label) in _main.AssignAreas())
        {
            var mi = new MenuItem { Header = label };
            mi.Click += (_, _) => _main.StartAssign(item, index);
            AssignMenu.Items.Add(mi);
        }
        AssignMenu.PlacementTarget = AssignButton;
        AssignMenu.IsOpen = true;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        ExportMenu.PlacementTarget = ExportButton;
        ExportMenu.IsOpen = true;
    }

    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { FileName = IconName() + ".png", Filter = "PNG (*.png)|*.png" };
        if (dlg.ShowDialog(_main) != true) return;
        File.WriteAllBytes(dlg.FileName, DesignRenderer.RenderPng(_d, _style));
        ShowSaved(dlg.FileName);
    }

    private void ExportIco_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { FileName = IconName() + ".ico", Filter = Loc.T("Windows-Icon (*.ico)|*.ico", "Windows icon (*.ico)|*.ico") };
        if (dlg.ShowDialog(_main) != true) return;
        IcoWriter.FromImage(DesignRenderer.RenderPng(_d, _style), dlg.FileName);
        ShowSaved(dlg.FileName);
    }

    private void ShowSaved(string file)
    {
        SavedText.Text = Loc.T("Exportiert: ", "Exported: ") + file;
        SavedText.Visibility = Visibility.Visible;
    }

    // ===================================================================== Helpers

    private void UpdateTemplatePreviews()
    {
        foreach (var (template, image) in _templatePreviews)
        {
            var copy = _d.Clone();
            template.Apply(copy);
            try { image.Source = DesignRenderer.Render(copy, _style); } catch (Exception ex) { App.LogError(ex); }
        }
    }


    /// <summary>Screenshot mode: a richer example (gradient squircle, symbol with long shadow, badge).</summary>
    internal void ShowExample()
    {
        _loadingGen = true;
        GenName.Text = Loc.T("Musik", "Music");
        _loadingGen = false;
        RollGenerator(newSeed: false);
        _d = MakerDesign.Default();
        _d.Shape = TileShape.Squircle;
        _d.UsePackPlate = false;
        _d.Layers.Add(new MakerLayer { Kind = LayerKind.Badge, Text = "3", Size = 0.3, X = 0.3, Y = -0.3 });
        MakerDesign.Templates()[2].Apply(_d);
        _layer = _d.Layers[0];
        var material = IconFonts.Get("material");
        if (material.Search("gamepad").FirstOrDefault() is { } pad) { _layer.GlyphSet = material.Id; _layer.Glyph = pad; }
        GlyphSearch.Text = Loc.T("spiel", "game");
        PushToUi();
        Redraw();
    }
}
