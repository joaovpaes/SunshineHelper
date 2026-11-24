using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SunshineHelper.Domain.Devices;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.App;

/// <summary>The Devices screen (docs/adr/0014-registro-de-aparelhos.md): the devices that
/// have connected, as Moonlight names them. The list itself lives in
/// <c>DeviceRegistry</c> (Application); this file only shows it.</summary>
public partial class MainWindow
{
    /// <summary>The registry builds a new list on every change, so comparing the reference is
    /// enough to know whether the screen is already up to date.</summary>
    private IReadOnlyList<KnownDevice>? _devicesShown;

    private void LoadDevices(bool force)
    {
        var devices = _host.Devices.Devices;
        if (!force && ReferenceEquals(devices, _devicesShown))
        {
            return;
        }

        _devicesShown = devices;
        string? selected = (DevicesList.SelectedItem as ListBoxItem)?.Tag as string;

        DevicesList.Items.Clear();
        foreach (var device in devices)
        {
            string profile = device.LastProfileName ?? Text("Devices.NoProfile");
            string last = device.LastSeen.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            var item = new ListBoxItem
            {
                Tag = device.Name,
                Content = string.Format(CultureInfo.CurrentCulture, Text("Devices.Item"), device.Name, device.ConnectionCount, last, profile),
            };
            DevicesList.Items.Add(item);
            if (string.Equals(device.Name, selected, StringComparison.Ordinal))
            {
                DevicesList.SelectedItem = item;
            }
        }

        bool empty = devices.Count == 0;
        DevicesEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        DevicesList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        UpdateDeviceButtons();
    }

    /// <summary>Called by the one-second timer: a device that connects while the screen is
    /// open shows up by itself.</summary>
    private void RefreshDevicesIfVisible()
    {
        if (IsVisible && DevicesCard.Visibility == Visibility.Visible)
        {
            LoadDevices(force: false);
        }
    }

    private void OnDeviceSelected(object sender, SelectionChangedEventArgs e) => UpdateDeviceButtons();

    /// <summary>The profile button says "Open" when the device already has a profile and
    /// "Create" when it does not.</summary>
    private void UpdateDeviceButtons()
    {
        string? name = (DevicesList.SelectedItem as ListBoxItem)?.Tag as string;
        bool selected = name is not null;
        ForgetDeviceButton.IsEnabled = selected;
        DeviceProfileButton.IsEnabled = selected;

        bool hasProfile = selected && ClientProfileSelector.FindByClientName(_host.Settings.Current, name) is not null;
        DeviceProfileButton.Content = Text(hasProfile ? "Devices.OpenProfile" : "Devices.CreateProfile");
    }

    /// <summary>Opens the Profiles screen: on the device's own profile if it has one,
    /// otherwise on a new profile already named after the device. Nothing is saved until
    /// the user clicks Save profile.</summary>
    private void OnDeviceProfile(object sender, RoutedEventArgs e)
    {
        if (DevicesList.SelectedItem is not ListBoxItem { Tag: string name })
        {
            return;
        }

        var existing = ClientProfileSelector.FindByClientName(_host.Settings.Current, name);

        foreach (ListBoxItem nav in NavList.Items)
        {
            if (nav.Tag is "Profiles")
            {
                NavList.SelectedItem = nav;
            }
        }

        if (existing is not null)
        {
            LoadProfiles(existing.Id);
            ProfilesMessage.Text = string.Empty;
            return;
        }

        // The blank "(new profile)" entry, filled with the device's name.
        LoadProfiles(selectId: null);
        _loadingProfiles = true;
        ProfileList.SelectedIndex = 0;
        _loadingProfiles = false;
        FillProfileForm(null, ProfileForm.NewForDevice(name));
        ProfilesMessage.Text = string.Format(CultureInfo.CurrentCulture, Text("Devices.ProfileDraft"), name);
    }

    private async void OnForgetDevice(object sender, RoutedEventArgs e)
    {
        if (DevicesList.SelectedItem is not ListBoxItem { Tag: string name })
        {
            return;
        }

        await _host.Devices.ForgetAsync(name, CancellationToken.None);
        DevicesMessage.Text = Text("Devices.Forgotten");
        LoadDevices(force: true);
    }
}
