using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using RPOverlay.Core.Models;
using RPOverlay.Core.Providers;
using RPOverlay.Core.Services;
using RPOverlay.WPF.Services;

namespace RPOverlay.WPF
{
    public class ProfileViewModel : INotifyPropertyChanged
    {
        private bool _isActive;
        
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime LastUsedDate { get; set; }
        
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                OnPropertyChanged();
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class ProfileManagerWindow : Window
    {
        private readonly ProfilePathProvider _profilePathProvider;
        private readonly ProfileService _profileService;
        private string _currentProfileId;
        
        public event EventHandler<string>? ProfileChanged;

        public ProfileManagerWindow(ProfilePathProvider profilePathProvider)
        {
            InitializeComponent();
            
            _profilePathProvider = profilePathProvider ?? throw new ArgumentNullException(nameof(profilePathProvider));
            _profileService = _profilePathProvider.GetProfileService();
            _currentProfileId = _profilePathProvider.CurrentProfileId;
            
            LoadProfiles();
        }

        private void LoadProfiles()
        {
            var profiles = _profileService.LoadProfiles();
            
            var profileViewModels = profiles
                .OrderByDescending(p => p.LastUsedDate)
                .Select(p => new ProfileViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    LastUsedDate = p.LastUsedDate,
                    IsActive = p.Id == _currentProfileId
                })
                .ToList();
            
            ProfilesList.ItemsSource = profileViewModels;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            var profileName = NewProfileNameTextBox.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageDialogService.Show(
                    "Ange ett namn för profilen.",
                    "Felaktigt namn",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            try
            {
                var newProfile = _profileService.CreateProfile(profileName);
                NewProfileNameTextBox.Text = string.Empty;
                LoadProfiles();
                
                MessageDialogService.Show(
                    $"Profilen '{newProfile.Name}' har skapats!",
                    "Profil skapad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageDialogService.Show(
                    $"Kunde inte skapa profilen: {ex.Message}",
                    "Fel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void ActivateProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button)
                return;

            var profileId = button.Tag as string;
            if (string.IsNullOrWhiteSpace(profileId))
                return;

            try
            {
                // Check if it's already the active profile
                if (profileId == _currentProfileId)
                {
                    MessageDialogService.Show(
                        "Denna profil är redan aktiv.",
                        "Information",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    return;
                }

                var result = MessageDialogService.Show(
                    "Vill du byta till denna profil? Programmet kommer att ladda om.",
                    "Bekräfta profilbyte",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    _profilePathProvider.SetActiveProfile(profileId);
                    _currentProfileId = profileId;
                    
                    // Notify the main window
                    ProfileChanged?.Invoke(this, profileId);
                    
                    LoadProfiles();
                    
                    MessageDialogService.Show(
                        "Profilen har bytts! Programmet laddas om.",
                        "Profil aktiverad",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            }
            catch (Exception ex)
            {
                MessageDialogService.Show(
                    $"Kunde inte aktivera profilen: {ex.Message}",
                    "Fel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button)
                return;

            var profileId = button.Tag as string;
            if (string.IsNullOrWhiteSpace(profileId))
                return;

            // Don't allow deleting the default profile
            if (profileId.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                MessageDialogService.Show(
                    "Standardprofilen kan inte tas bort.",
                    "Åtgärd ej tillåten",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // Don't allow deleting the active profile
            if (profileId == _currentProfileId)
            {
                MessageDialogService.Show(
                    "Du kan inte ta bort den aktiva profilen. Byt till en annan profil först.",
                    "Åtgärd ej tillåten",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            var profile = _profileService.GetProfile(profileId);
            if (profile == null)
                return;

            var result = MessageDialogService.Show(
                $"Är du säker på att du vill ta bort profilen '{profile.Name}'?\n\nAlla anteckningar och kommandon kommer att raderas permanent.",
                "Bekräfta borttagning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var deleted = _profileService.DeleteProfile(profileId);
                    if (deleted)
                    {
                        LoadProfiles();
                        MessageDialogService.Show(
                            $"Profilen '{profile.Name}' har tagits bort.",
                            "Profil borttagen",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                    else
                    {
                        MessageDialogService.Show(
                            "Kunde inte ta bort profilen.",
                            "Fel",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }
                catch (Exception ex)
                {
                    MessageDialogService.Show(
                        $"Fel vid borttagning: {ex.Message}",
                        "Fel",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }
    }
}
