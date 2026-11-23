using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SunshineHelper.Domain.Settings;

namespace SunshineHelper.App;

/// <summary>The Profiles screen (docs/adr/0009-tela-de-perfis.md). The rules live in
/// <see cref="ProfileForm"/>; this file only moves text between the fields and it.</summary>
public partial class MainWindow
{
    /// <summary>Id of the profile on screen; null while a new one is being written.</summary>
    private string? _editingProfileId;

    private bool _loadingProfiles;

    private void LoadProfiles(string? selectId)
    {
        _loadingProfiles = true;
        var settings = _host.Settings.Current;

        ProfileList.Items.Clear();
        ProfileList.Items.Add(new ListBoxItem { Tag = null, Content = Text("Profiles.NewItem") });
        foreach (var profile in settings.Profiles)
        {
            ProfileList.Items.Add(new ListBoxItem { Tag = profile.Id, Content = profile.Name });
        }

        // Opens on the first real profile; with none yet, on the blank "new" entry.
        string? target = selectId ?? (settings.Profiles.Count > 0 ? settings.Profiles[0].Id : null);
        ProfileList.SelectedIndex = 0;
        foreach (ListBoxItem item in ProfileList.Items)
        {
            if (item.Tag is string id && string.Equals(id, target, StringComparison.Ordinal))
            {
                ProfileList.SelectedItem = item;
            }
        }

        _loadingProfiles = false;
        FillProfileForm(FindProfile(settings, (ProfileList.SelectedItem as ListBoxItem)?.Tag as string));
    }

    private static ClientProfile? FindProfile(AppSettings settings, string? id) =>
        id is null ? null : settings.Profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    private void FillProfileForm(ClientProfile? profile)
    {
        _loadingProfiles = true;
        _editingProfileId = profile?.Id;

        var input = profile is null ? new ProfileFormInput() : ProfileForm.FromProfile(profile);
        ProfileNameBox.Text = input.Name;
        ProfileClientsBox.Text = input.ClientNamesText;
        SelectMode(ResolutionModeBox, input.ResolutionMode);
        ResolutionBox.Text = input.ResolutionText;
        SelectMode(HdrModeBox, input.HdrMode);
        HdrOnBox.IsChecked = input.HdrOn;
        SelectMode(FpsModeBox, input.FpsMode);
        FpsBox.Text = input.FpsText;
        RevertOnDisconnectBox.IsChecked = input.RevertOnDisconnect;
        ProfileDelayBox.Text = input.LauncherDelayText;
        DefaultProfileBox.IsChecked = profile is not null
            && string.Equals(_host.Settings.Current.DefaultProfileId, profile.Id, StringComparison.Ordinal);

        DeleteProfileButton.IsEnabled = profile is not null;
        ProfilesMessage.Text = string.Empty;
        _loadingProfiles = false;
        UpdateModeFields();
    }

    private ProfileFormInput ReadProfileForm() => new()
    {
        Name = ProfileNameBox.Text,
        ClientNamesText = ProfileClientsBox.Text,
        ResolutionMode = ModeOf(ResolutionModeBox),
        ResolutionText = ResolutionBox.Text,
        HdrMode = ModeOf(HdrModeBox),
        HdrOn = HdrOnBox.IsChecked == true,
        FpsMode = ModeOf(FpsModeBox),
        FpsText = FpsBox.Text,
        RevertOnDisconnect = RevertOnDisconnectBox.IsChecked == true,
        LauncherDelayText = ProfileDelayBox.Text,
    };

    private void OnProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingProfiles || ProfileList.SelectedItem is not ListBoxItem item)
        {
            return;
        }

        FillProfileForm(FindProfile(_host.Settings.Current, item.Tag as string));
    }

    private void OnNewProfile(object sender, RoutedEventArgs e) => ProfileList.SelectedIndex = 0;

    private void OnProfileModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingProfiles)
        {
            UpdateModeFields();
        }
    }

    /// <summary>The value boxes only matter in "Fixed value" mode.</summary>
    private void UpdateModeFields()
    {
        ResolutionBox.IsEnabled = ModeOf(ResolutionModeBox) == ProfileValueMode.Fixed;
        HdrOnBox.IsEnabled = ModeOf(HdrModeBox) == ProfileValueMode.Fixed;
        FpsBox.IsEnabled = ModeOf(FpsModeBox) == ProfileValueMode.Fixed;
    }

    private async void OnSaveProfile(object sender, RoutedEventArgs e)
    {
        var current = _host.Settings.Current;
        var existing = FindProfile(current, _editingProfileId);
        var otherIds = current.Profiles.Select(p => p.Id).ToList();

        var result = ProfileForm.Build(ReadProfileForm(), existing, otherIds);
        if (result.Profile is not { } profile)
        {
            ProfilesMessage.Text = string.Join(' ', result.Errors.Select(error => Text(error switch
            {
                ProfileFormError.NameEmpty => "Profiles.Error.Name",
                ProfileFormError.ResolutionInvalid => "Profiles.Error.Resolution",
                ProfileFormError.FpsInvalid => "Profiles.Error.Fps",
                _ => "Profiles.Error.Delay",
            })));
            return;
        }

        bool makeDefault = DefaultProfileBox.IsChecked == true;
        if (await TrySaveProfilesAsync(settings => WithProfile(settings, profile, makeDefault)))
        {
            LoadProfiles(profile.Id);
            ProfilesMessage.Text = Text("Profiles.Saved");
        }
    }

    private static AppSettings WithProfile(AppSettings settings, ClientProfile profile, bool makeDefault)
    {
        var profiles = settings.Profiles.ToList();
        int index = profiles.FindIndex(p => string.Equals(p.Id, profile.Id, StringComparison.Ordinal));
        if (index >= 0)
        {
            profiles[index] = profile;
        }
        else
        {
            profiles.Add(profile);
        }

        bool wasDefault = string.Equals(settings.DefaultProfileId, profile.Id, StringComparison.Ordinal);
        string? defaultId = makeDefault ? profile.Id : wasDefault ? null : settings.DefaultProfileId;
        return settings with { Profiles = profiles, DefaultProfileId = defaultId };
    }

    private async void OnDeleteProfile(object sender, RoutedEventArgs e)
    {
        var profile = FindProfile(_host.Settings.Current, _editingProfileId);
        if (profile is null)
        {
            return;
        }

        var answer = System.Windows.MessageBox.Show(
            this,
            string.Format(CultureInfo.CurrentCulture, Text("Profiles.DeleteConfirm"), profile.Name),
            Text("Profiles.DeleteTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        string id = profile.Id;
        if (await TrySaveProfilesAsync(settings => settings with
        {
            Profiles = settings.Profiles.Where(p => !string.Equals(p.Id, id, StringComparison.Ordinal)).ToList(),
            DefaultProfileId = string.Equals(settings.DefaultProfileId, id, StringComparison.Ordinal)
                ? null
                : settings.DefaultProfileId,
        }))
        {
            LoadProfiles(selectId: null);
            ProfilesMessage.Text = Text("Profiles.Deleted");
        }
    }

    /// <summary>Same as <see cref="TrySaveAsync"/>, but the error goes to this screen's message line.</summary>
    private async Task<bool> TrySaveProfilesAsync(Func<AppSettings, AppSettings> change)
    {
        bool saved = await TrySaveAsync(change);
        if (!saved)
        {
            ProfilesMessage.Text = GeneralMessage.Text;
            GeneralMessage.Text = string.Empty;
        }

        return saved;
    }

    private static void SelectMode(System.Windows.Controls.ComboBox box, ProfileValueMode mode)
    {
        string tag = mode.ToString();
        foreach (ComboBoxItem item in box.Items)
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.Ordinal))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private static ProfileValueMode ModeOf(System.Windows.Controls.ComboBox box) =>
        box.SelectedItem is ComboBoxItem { Tag: string tag } && Enum.TryParse(tag, out ProfileValueMode mode)
            ? mode
            : ProfileValueMode.Leave;
}
