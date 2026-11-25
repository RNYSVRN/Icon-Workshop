using System.Windows;
using System.Windows.Input;

namespace IconWorkshop.Controls;

public enum DialogResultKind { Primary, Secondary, Close }

/// <summary>
/// Confirmation in the style of the Windows 11 ContentDialog instead of the grey system MessageBox.
/// Enter = primary action, Esc = close. While the dialog is open, the main window is dimmed.
/// </summary>
public partial class FluentDialog : Window
{
    public const string GlyphInfo = "";
    public const string GlyphWarning = "";
    public const string GlyphError = "";
    public const string GlyphEdit = "";

    private DialogResultKind _result = DialogResultKind.Close;

    private FluentDialog() => InitializeComponent();

    /// <summary>Owner window that dims itself while a dialog is open.</summary>
    public interface IDimmable { void SetSmoke(bool on); }

    public static DialogResultKind Show(Window? owner, string title, string message, string primary,
        string? secondary = null, string? close = null, string? note = null, string glyph = GlyphInfo)
    {
        var dlg = Create(owner, title, message, primary, secondary, close, note, glyph);
        return dlg.Run(owner);
    }

    /// <summary>Text input (e.g. renaming). Returns null when cancelled.</summary>
    public static string? Prompt(Window? owner, string title, string message, string initial, string primary)
    {
        var dlg = Create(owner, title, message, primary, null, Loc.Cancel, null, GlyphEdit);
        dlg.InputBox.Text = initial;
        dlg.InputBox.Visibility = Visibility.Visible;
        dlg.Loaded += (_, _) => { dlg.InputBox.Focus(); dlg.InputBox.SelectAll(); };
        return dlg.Run(owner) == DialogResultKind.Primary ? dlg.InputBox.Text.Trim() : null;
    }

    private static FluentDialog Create(Window? owner, string title, string message, string primary,
        string? secondary, string? close, string? note, string glyph)
    {
        var dlg = new FluentDialog { Title = title };
        dlg.TitleText.Text = title;
        dlg.MessageText.Text = message;
        dlg.MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        dlg.IconGlyph.Text = glyph;
        if (!string.IsNullOrEmpty(note)) { dlg.NoteText.Text = note; dlg.NoteText.Visibility = Visibility.Visible; }

        dlg.PrimaryButton.Content = primary;
        dlg.SecondaryButton.Content = secondary;
        dlg.CloseButton.Content = close;
        if (secondary is null) dlg.Buttons.Children.Remove(dlg.SecondaryButton);
        if (close is null) dlg.Buttons.Children.Remove(dlg.CloseButton);
        dlg.Buttons.Columns = dlg.Buttons.Children.Count;
        return dlg;
    }

    private DialogResultKind Run(Window? owner)
    {
        bool hasOwner = owner is { IsVisible: true };
        if (hasOwner) Owner = owner;
        else { WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; }

        (owner as IDimmable)?.SetSmoke(true);
        try { ShowDialog(); }
        finally { (owner as IDimmable)?.SetSmoke(false); }
        return _result;
    }

    private void Primary_Click(object sender, RoutedEventArgs e) { _result = DialogResultKind.Primary; Close(); }
    private void Secondary_Click(object sender, RoutedEventArgs e) { _result = DialogResultKind.Secondary; Close(); }
    private void Close_Click(object sender, RoutedEventArgs e) { _result = DialogResultKind.Close; Close(); }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        try { DragMove(); } catch { /* only possible while the mouse button is pressed */ }
    }
}
