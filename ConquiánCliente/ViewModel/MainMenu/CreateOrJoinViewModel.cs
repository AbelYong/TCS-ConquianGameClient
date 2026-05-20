using ConquiánCliente.Properties.Langs;
using ServiceLobby;
using ConquiánCliente.Utilities.Messages;
using ConquiánCliente.ViewModel.Lobby;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ConquiánCliente.ViewModel.MainMenu
{
    public class CreateOrJoinViewModel : ViewModelBase
    {
        private string roomCode;
        private bool isLoading;
        private readonly IMessageResolver messageResolver;

        public string RoomCode
        {
            get { return roomCode; }
            set
            {
                roomCode = value;
                OnPropertyChanged(nameof(RoomCode));
            }
        }
        public string CreatedRoomCode { get; private set; }

        public ICommand CreateRoomCommand { get; }
        public ICommand JoinRoomCommand { get; }
        public ICommand CloseCommand { get; }

        public CreateOrJoinViewModel()
        {
            this.messageResolver = new ResourceMessageResolver();
            isLoading = false;

            CreateRoomCommand = new RelayCommand(ExecuteCreateRoom, CanExecuteSubmit);
            JoinRoomCommand = new RelayCommand(ExecuteJoinRoom, CanExecuteSubmit);
            CloseCommand = new RelayCommand(ExecuteClose);
        }

        private bool CanExecuteSubmit(object parameter)
        {
            return !isLoading;
        }

        private async void ExecuteCreateRoom(object parameter)
        {
            if (isLoading || !(parameter is Window window))
            {
                return;
            }

            SetLoadingState(true);

            var context = new InstanceContext(LobbyCallbackHandler.Instance);
            var tcpBinding = new NetTcpBinding(SecurityMode.None);
            var endpoint = new EndpointAddress("net.tcp://localhost:8081/lobby");

            var factory = new DuplexChannelFactory<ILobby>(context, tcpBinding, endpoint);
            ILobby client = factory.CreateChannel();
            ((ICommunicationObject)client).Open();

            try
            {
                await AttemptCreateLobby(client, window);
            }
            catch (FaultException<ServiceLobby.ServiceFaultDto> ex)
            {
                HandleServiceFault(ex, isInfo: true);
            }
            catch (EndpointNotFoundException)
            {
                ShowConnectionError(Lang.ErrorServerUnavailable);
            }
            catch (TimeoutException)
            {
                ShowConnectionError(Lang.ErrorConnectingToServer);
            }
            catch (CommunicationException)
            {
                ShowConnectionError(Lang.ErrorConnectingToServer);
            }
            finally
            {
                SafeCloseClient(client, factory);
                SetLoadingState(false);
            }
        }



        private void HandleServiceException(Exception ex)
        {
            if (ex is FaultException<ServiceLobby.ServiceFaultDto> fault)
            {
                var errorType = (ServiceLogin.ServiceErrorType)(int)fault.Detail.ErrorType;
                string msg = messageResolver.GetMessage(errorType);
                MessageBox.Show(msg, Lang.TitleError, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (ex is EndpointNotFoundException)
            {
                MessageBox.Show(Lang.ErrorServerUnavailable, Lang.TitleConnectionError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (ex is CommunicationException)
            {
                MessageBox.Show(Lang.ErrorConnectingToServer, Lang.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(Lang.ErrorConnectingToServer, Lang.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AttemptCreateLobby(ILobby client, Window window)
        {
            CreatedRoomCode = await client.CreateLobbyAsync(PlayerSession.CurrentPlayer.idPlayer);

            if (!string.IsNullOrEmpty(CreatedRoomCode))
            {
                CloseWindowWithResult(window, true);
            }
            else
            {
                MessageBox.Show(Lang.ErrorLobbyCreation, Lang.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExecuteJoinRoom(object parameter)
        {
            if (string.IsNullOrWhiteSpace(RoomCode))
            {
                MessageBox.Show(Lang.ErrorEmptyRoomCode, Lang.TitleError, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (isLoading || !(parameter is Window window))
            {
                return;
            }

            SetLoadingState(true);

            var context = new InstanceContext(LobbyCallbackHandler.Instance);
            var tcpBinding = new NetTcpBinding(SecurityMode.None);
            var endpoint = new EndpointAddress("net.tcp://localhost:8081/lobby");

            var factory = new DuplexChannelFactory<ILobby>(context, tcpBinding, endpoint);
            ILobby client = factory.CreateChannel();
            ((ICommunicationObject)client).Open();

            try
            {
                await AttemptJoinLobby(client, window);
            }
            catch (FaultException<ServiceLobby.ServiceFaultDto> ex)
            {
                HandleServiceFault(ex, isInfo: true);
            }
            catch (EndpointNotFoundException)
            {
                ShowConnectionError(Lang.ErrorServerUnavailable);
            }
            catch (TimeoutException)
            {
                ShowConnectionError(Lang.ErrorConnectingToServer);
            }
            catch (CommunicationException)
            {
                ShowConnectionError(Lang.ErrorConnectingToServer);
            }
            finally
            {
                SafeCloseClient(client, factory);
                SetLoadingState(false);
            }
        }

        private async Task AttemptJoinLobby(ILobby client, Window window)
        {
            var lobbyState = await client.GetLobbyStateAsync(RoomCode.ToUpper());

            if (lobbyState != null)
            {
                CreatedRoomCode = RoomCode.ToUpper();
                CloseWindowWithResult(window, true);
            }
        }

        private void SetLoadingState(bool loading)
        {
            isLoading = loading;
            CommandManager.InvalidateRequerySuggested();
        }

        private void SafeCloseClient(ILobby client, DuplexChannelFactory<ILobby> factory)
        {
            if (client != null)
            {
                try
                {
                    if (((ICommunicationObject)client).State == CommunicationState.Opened)
                    {
                        ((ICommunicationObject)client).Close();
                    }
                    else
                    {
                        ((ICommunicationObject)client).Abort();
                    }
                }
                catch (CommunicationException)
                {
                    ((ICommunicationObject)client).Abort();
                }
                catch (TimeoutException)
                {
                    ((ICommunicationObject)client).Abort();
                }
                catch (Exception)
                {
                    ((ICommunicationObject)client).Abort();
                }
            }

            if (factory != null)
            {
                try { factory.Close(); } catch { factory.Abort(); }
            }
        }

        private void CloseWindowWithResult(Window window, bool result)
        {
            window.DialogResult = result;
            window.Close();
        }

        private void HandleServiceFault(FaultException<ServiceLobby.ServiceFaultDto> fault, bool isInfo = false)
        {
            var errorType = (ServiceLogin.ServiceErrorType)(int)fault.Detail.ErrorType;
            string msg = messageResolver.GetMessage(errorType);

            MessageBoxImage icon = isInfo ? MessageBoxImage.Information : MessageBoxImage.Warning;
            string title = isInfo ? Lang.TitleInfo : Lang.TitleError;

            MessageBox.Show(msg, title, MessageBoxButton.OK, icon);
        }

        private void ShowConnectionError(string message)
        {
            MessageBox.Show(message, Lang.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void ExecuteClose(object parameter)
        {
            if (parameter is Window window)
            {
                window.Close();
            }
        }
    }
}