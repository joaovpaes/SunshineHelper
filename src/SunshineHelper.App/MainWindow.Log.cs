using System.Diagnostics;
using System.IO;
using System.Windows;
using SunshineHelper.Application.Diagnostics;
using SunshineHelper.Infrastructure.Diagnostics;

namespace SunshineHelper.App;

/// <summary>The Log screen: the end of the app's own log (app.log), read-only, with an
/// optional "only warnings and errors" filter. It only reads the file; nothing is changed.</summary>
public partial class MainWindow
{
    /// <summary>How many lines of the end of app.log the screen shows.</summary>
    private const int LogViewLineLimit = 500;

    private string? _logViewShown;

    private void LoadLogView(bool force)
    {
        var lines = DiagnosticFiles.ReadLastLines(FileDiagnosticLog.DefaultPath, LogViewLineLimit);
        string text = lines is null
            ? Text("LogView.Unavailable")
            : LogViewFilter.Join(LogViewFilter.Apply(lines, LogOnlyProblemsBox.IsChecked == true));

        if (!force && string.Equals(text, _logViewShown, StringComparison.Ordinal))
        {
            return;
        }

        // Keep the reading position unless the user was already at the end.
        bool atEnd = force || LogViewBox.VerticalOffset + LogViewBox.ViewportHeight >= LogViewBox.ExtentHeight - 1;
        _logViewShown = text;
        LogViewBox.Text = text;
        if (atEnd)
        {
            LogViewBox.ScrollToEnd();
        }
    }

    /// <summary>Called by the one-second timer: new lines show up by themselves.</summary>
    private void RefreshLogViewIfVisible()
    {
        if (IsVisible && LogCard.Visibility == Visibility.Visible)
        {
            LoadLogView(force: false);
        }
    }

    private void OnLogFilterChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            LoadLogView(force: true);
        }
    }

    private void OnLogCopy(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(LogViewBox.Text))
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(LogViewBox.Text);
            LogViewMessage.Text = Text("LogView.Copied");
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another program is holding the clipboard.
            LogViewMessage.Text = Text("LogView.CopyFailed");
        }
    }

    private void OnLogOpenFolder(object sender, RoutedEventArgs e)
    {
        string? folder = Path.GetDirectoryName(FileDiagnosticLog.DefaultPath);
        if (folder is null || !Directory.Exists(folder))
        {
            LogViewMessage.Text = Text("LogView.NoFolder");
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true })?.Dispose();
    }
}
