using ServicePresence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Windows;
using ConquiánCliente.Properties.Langs;
using ConquiánCliente.View.Authentication;

namespace ConquiánCliente.ViewModel
{
    public class PresenceClientManager
    {
        private static PresenceClientManager instance;

        // --- CAMBIO: Usamos IPresence y DuplexChannelFactory ---
        private IPresence client;
        private DuplexChannelFactory<IPresence> factory;
        // -------------------------------------------------------

        public IPresence Client
        {
            get
            {
                if (IsClientInvalid())
                {
                    InitializeClient();
                }
                return client;
            }
        }

        private PresenceClientManager()
        {
        }

        public static PresenceClientManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PresenceClientManager();
                }
                return instance;
            }
        }

        private bool IsClientInvalid()
        {
            // --- CAMBIO: Casteo a ICommunicationObject ---
            bool isInvalid = client == null ||
                             ((ICommunicationObject)client).State == CommunicationState.Closed ||
                             ((ICommunicationObject)client).State == CommunicationState.Faulted;
            return isInvalid;
        }

        private void InitializeClient()
        {
            CleanupExistingClient();

            var context = new InstanceContext(new PresenceCallbackHandler());

            // --- CAMBIO APLICADO AQUÍ PARA .NET 8 (Conexión TCP / Duplex) ---
            var tcpBinding = new NetTcpBinding(SecurityMode.None);
            var endpoint = new EndpointAddress("net.tcp://localhost:8081/presence");

            factory = new DuplexChannelFactory<IPresence>(context, tcpBinding, endpoint);
            client = factory.CreateChannel();

            // Abrimos el canal explícitamente
            ((ICommunicationObject)client).Open();
            // ----------------------------------------------------------------

            if (client != null)
            {
                ((ICommunicationObject)client).Closed += OnConnectionLost;
                ((ICommunicationObject)client).Faulted += OnConnectionLost;
            }
        }

        private void CleanupExistingClient()
        {
            if (client == null)
            {
                return;
            }

            try
            {
                ((ICommunicationObject)client).Closed -= OnConnectionLost;
                ((ICommunicationObject)client).Faulted -= OnConnectionLost;

                if (((ICommunicationObject)client).State == CommunicationState.Faulted)
                {
                    ((ICommunicationObject)client).Abort();
                }
                else
                {
                    ((ICommunicationObject)client).Close();
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
            finally
            {
                if (factory != null)
                {
                    try { factory.Close(); } catch { factory.Abort(); }
                }
                client = null;
                factory = null;
            }
        }

        private static void OnConnectionLost(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ShouldSkipDisconnectionHandling())
                {
                    return;
                }

                HandleDisconnectionSequence();
            });
        }

        private static bool ShouldSkipDisconnectionHandling()
        {
            bool skip = PlayerSession.IsNetworkDown || !PlayerSession.IsLoggedIn;
            return skip;
        }

        private static void HandleDisconnectionSequence()
        {
            PlayerSession.IsNetworkDown = true;

            MessageBox.Show(Lang.ErrorLostConnection, Lang.TitleError, MessageBoxButton.OK, MessageBoxImage.Warning);

            TransitionToLogin();

            PlayerSession.IsNetworkDown = false;
        }

        private static void TransitionToLogin()
        {
            LogIn loginWindow = new LogIn();
            loginWindow.Show();

            CloseOtherWindows(loginWindow);

            Application.Current.MainWindow = loginWindow;
            PlayerSession.EndSession();
        }

        private static void CloseOtherWindows(Window keepOpenWindow)
        {
            List<Window> windowsToClose = Application.Current.Windows.OfType<Window>()
                .Where(w => w != keepOpenWindow)
                .ToList();

            foreach (Window window in windowsToClose)
            {
                window.Close();
            }
        }
    }
}