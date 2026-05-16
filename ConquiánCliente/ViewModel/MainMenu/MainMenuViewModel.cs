using ConquiánCliente.Properties.Langs;
using ServiceLogin;
using ConquiánCliente.View;
using ConquiánCliente.View.Lobby;
using ConquiánCliente.View.MainMenu;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ConquiánCliente.ViewModel.MainMenu
{
    public class MainMenuViewModel : ViewModelBase
    {
        // --- CAMBIO: Propiedades completas con OnPropertyChanged para que WPF actualice la vista ---
        private string nickname;
        public string Nickname
        {
            get => nickname;
            set { nickname = value; OnPropertyChanged(nameof(Nickname)); }
        }

        private string profileImagePath;
        public string ProfileImagePath
        {
            get => profileImagePath;
            set { profileImagePath = value; OnPropertyChanged(nameof(ProfileImagePath)); }
        }
        // ------------------------------------------------------------------------------------------

        public ICommand ViewProfileCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand FriendsCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        private bool isLoading;

        public MainMenuViewModel()
        {
            LoadPlayerData();
            isLoading = false;

            ViewProfileCommand = new RelayCommand(ExecuteViewProfileCommand, CanExecuteNavigation);
            LogoutCommand = new RelayCommand(ExecuteLogoutCommand, CanExecuteNavigation);
            FriendsCommand = new RelayCommand(ExecuteFriendsCommand, CanExecuteNavigation);
            PlayCommand = new RelayCommand(ExecutePlay, CanExecuteNavigation);
            OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings, CanExecuteNavigation);

            InitializeBackgroundConnections();
        }

        private void InitializeBackgroundConnections()
        {
            if (PlayerSession.CurrentPlayer != null)
            {
                int playerId = PlayerSession.CurrentPlayer.idPlayer;
                Task.Run(async () => await AttemptConnectionSetup(playerId));
            }
        }

        private static async Task AttemptConnectionSetup(int playerId)
        {
            try
            {
                InvitationClientManager.Connect(playerId);
                if (PresenceClientManager.Instance.Client != null)
                {
                    await PresenceClientManager.Instance.Client.SubscribeAsync(PlayerSession.CurrentPlayer.idPlayer);
                }
            }
            catch (CommunicationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network error connecting services: {ex.Message}");
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timeout connecting services: {ex.Message}");
            }
        }

        private bool CanExecuteNavigation(object parameter)
        {
            return !isLoading;
        }

        private void LoadPlayerData()
        {
            if (PlayerSession.IsLoggedIn)
            {
                Nickname = PlayerSession.CurrentPlayer.nickname;

                string imageName = System.IO.Path.GetFileName(PlayerSession.CurrentPlayer.pathPhoto);
                ProfileImagePath = $"pack://application:,,,/Resources/imageProfile/{imageName}";
            }
        }

        private void ExecuteViewProfileCommand(object parameter)
        {
            if (isLoading)
            {
                return;
            }

            SetLoadingState(true);

            ProfileMainFrame userProfileView = ProfileMainFrame.GetInstance();
            userProfileView.Show();

            CloseWindow(parameter as Window);
        }

        private async void ExecuteLogoutCommand(object parameter)
        {
            if (isLoading)
            {
                return;
            }

            SetLoadingState(true);

            await PerformServerLogoutAsync();
            TransitionToLogin(parameter as Window);
        }

        private async Task PerformServerLogoutAsync()
        {
            int playerId = PlayerSession.CurrentPlayer.idPlayer;

            await Task.Run(async () =>
            {
                await SignOutLoginService(playerId);
                await UnsubscribePresenceService(playerId);
                DisconnectInvitationService(playerId);
            });
        }

        private async Task SignOutLoginService(int playerId)
        {
            // --- CAMBIO: Usamos 127.0.0.1 ---
            var basicBinding = new BasicHttpBinding(BasicHttpSecurityMode.None);
            var endpoint = new EndpointAddress("http://127.0.0.1:8080/login");
            var loginClient = new LoginClient(basicBinding, endpoint);
            // --------------------------------
            try
            {
                await loginClient.SignOutPlayerAsync(playerId);
            }
            catch (CommunicationException)
            {
            }
            catch (TimeoutException)
            {
            }
        }

        private async Task UnsubscribePresenceService(int playerId)
        {
            try
            {
                if (PresenceClientManager.Instance.Client != null)
                {
                    await PresenceClientManager.Instance.Client.UnsubscribeAsync(playerId);
                }
            }
            catch (CommunicationException)
            {
            }
            catch (TimeoutException)
            {
            }
        }

        private void DisconnectInvitationService(int playerId)
        {
            try
            {
                InvitationClientManager.Disconnect(playerId);
            }
            catch (Exception)
            {
            }
        }

        private void TransitionToLogin(Window currentWindow)
        {
            PlayerSession.EndSession();
            var loginWindow = new LogIn();
            loginWindow.Show();

            CloseWindow(currentWindow);
        }

        private void ExecuteFriendsCommand(object obj)
        {
            if (isLoading)
            {
                return;
            }

            SetLoadingState(true);

            if (obj is Window mainMenuWindow)
            {
                var friendListWindow = new View.FriendList.FriendList();
                friendListWindow.Show();
                mainMenuWindow.Close();
            }
        }

        private void ExecutePlay(object parameter)
        {
            if (isLoading)
            {
                return;
            }

            SetLoadingState(true);

            if (parameter is Window currentWindow)
            {
                string newRoomCode = TryGetRoomCodeFromDialog(currentWindow);

                if (!string.IsNullOrEmpty(newRoomCode))
                {
                    NavigateToLobby(newRoomCode, currentWindow);
                    return;
                }
            }

            SetLoadingState(false);
        }

        private string TryGetRoomCodeFromDialog(Window owner)
        {
            string code = string.Empty;
            CreateOrJoin createOrJoinView = new CreateOrJoin();
            createOrJoinView.Owner = owner;

            bool? result = createOrJoinView.ShowDialog();

            if (result == true)
            {
                var createJoinViewModel = createOrJoinView.DataContext as CreateOrJoinViewModel;
                if (createJoinViewModel != null)
                {
                    code = createJoinViewModel.CreatedRoomCode;
                }
            }

            return code;
        }

        private void NavigateToLobby(string roomCode, Window currentWindow)
        {
            LobbyGame lobby = new LobbyGame(roomCode);
            lobby.Show();
            currentWindow.Close();
        }

        private void ExecuteOpenSettings(object parameter)
        {
            if (isLoading)
            {
                return;
            }

            SetLoadingState(true);

            if (parameter is Window currentWindow)
            {
                var settingsView = new Settings();
                settingsView.Owner = currentWindow;
                settingsView.ShowDialog();
            }

            SetLoadingState(false);
        }

        private void SetLoadingState(bool loading)
        {
            isLoading = loading;
            CommandManager.InvalidateRequerySuggested();
        }

        private void CloseWindow(Window window)
        {
            if (window != null)
            {
                window.Close();
            }
        }
    }
}