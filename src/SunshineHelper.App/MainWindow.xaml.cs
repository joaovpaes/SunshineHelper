using System.Windows;
using System.Windows.Controls;

namespace SunshineHelper.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Not set in XAML: SelectionChanged would fire before PageTitle exists.
        NavList.SelectedIndex = 0;
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem { Tag: string page })
        {
            return;
        }

        // SetResourceReference, not a copied string: the text keeps following the language
        // if it is switched while the window is open.
        PageTitle.SetResourceReference(TextBlock.TextProperty, $"Page.{page}.Title");
        PageDescription.SetResourceReference(TextBlock.TextProperty, $"Page.{page}.Description");
    }
}
