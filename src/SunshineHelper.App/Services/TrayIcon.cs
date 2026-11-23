using System.IO;

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
            Icon = LoadIcon(),
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

    /// <summary>Loads the small size from the icon embedded in the assembly; falls back to the
    /// system icon so a resource problem can never keep the app out of the tray.</summary>
    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            if (info is not null)
            {
                using var stream = info.Stream;
                return new System.Drawing.Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UriFormatException)
        {
            // Falls through to the system icon.
        }

        return System.Drawing.SystemIcons.Application;
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
