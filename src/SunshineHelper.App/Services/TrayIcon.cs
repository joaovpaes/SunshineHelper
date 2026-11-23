namespace SunshineHelper.App.Services;

/// <summary>
/// The notification-area icon. WPF has no tray control of its own, so this wraps
/// <see cref="System.Windows.Forms.NotifyIcon"/>. Menu texts are looked up in the
/// string tables each time the menu opens, so they follow a language switch.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Windows.Forms.ToolStripMenuItem _openItem = new();
    private readonly System.Windows.Forms.ToolStripMenuItem _exitItem = new();

    public TrayIcon(Action open, Action exit)
    {
        _openItem.Click += (_, _) => open();
        _exitItem.Click += (_, _) => exit();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(_openItem);
        menu.Items.Add(_exitItem);
        menu.Opening += (_, _) => RefreshTexts();

        _icon = new System.Windows.Forms.NotifyIcon
        {
            // Placeholder: the real icon is a design asset that comes with the fonts.
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => open();
        RefreshTexts();
    }

    public void Dispose()
    {
        // Left visible, the icon would linger in the tray until the mouse passes over it.
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _openItem.Dispose();
        _exitItem.Dispose();
    }

    private static string Text(string key) =>
        System.Windows.Application.Current.TryFindResource(key) as string ?? key;

    private void RefreshTexts()
    {
        _openItem.Text = Text("Tray.Open");
        _exitItem.Text = Text("Tray.Exit");
        _icon.Text = Text("App.Title");
    }
}
