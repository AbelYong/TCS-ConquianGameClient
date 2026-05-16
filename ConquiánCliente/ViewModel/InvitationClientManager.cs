using ConquiánCliente.Properties.Langs;
using ServiceInvitation;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;

namespace ConquiánCliente.ViewModel
{
    public static class InvitationClientManager
    {
        // --- CAMBIO: Usamos IInvitationService en lugar de InvitationServiceClient ---
        private static IInvitationService client;
        private static DuplexChannelFactory<IInvitationService> factory;
        // -----------------------------------------------------------------------------

        public static void Connect(int idPlayer)
        {
            if (IsClientConnected())
            {
                return;
            }

            try
            {
                var callbackHandler = new InvitationCallbackHandler();
                var context = new InstanceContext(callbackHandler);

                // --- CAMBIO APLICADO AQUÍ PARA .NET 8 (Conexión TCP / Duplex) ---
                var tcpBinding = new NetTcpBinding(SecurityMode.None);
                var endpoint = new EndpointAddress("net.tcp://localhost:8081/invitation");

                factory = new DuplexChannelFactory<IInvitationService>(context, tcpBinding, endpoint);
                client = factory.CreateChannel();

                // Abrimos el canal explícitamente para que el estado cambie a Opened
                ((ICommunicationObject)client).Open();
                // ----------------------------------------------------------------

                // Enviamos la suscripción en segundo plano usando la versión Async
                Task.Run(async () =>
                {
                    try
                    {
                        await client.SubscribeAsync(idPlayer);
                    }
                    catch { /* Ignoramos si falla en segundo plano para no crashear */ }
                });
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
        }

        public static void Disconnect(int idPlayer)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                if (((ICommunicationObject)client).State == CommunicationState.Opened)
                {
                    // --- CAMBIO: Usamos la versión Async envuelta en un Task ---
                    Task.Run(async () =>
                    {
                        try
                        {
                            await client.UnsubscribeAsync(idPlayer);
                        }
                        catch { }
                    });
                }
            }
            catch (CommunicationException)
            {
            }
            catch (TimeoutException)
            {
            }
            finally
            {
                SafeCloseClient();
            }
        }

        public static async Task SendInvitationAsync(InvitationSenderDto sender, int idReceiver, string roomCode)
        {
            if (!IsClientConnected())
            {
                Connect(sender.IdPlayer);
            }

            if (IsClientConnected())
            {
                await client.SendInvitationAsync(sender, idReceiver, roomCode);
            }
            else
            {
                throw new CommunicationException(Lang.ErrorConnectingToServer);
            }
        }

        private static bool IsClientConnected()
        {
            // --- CAMBIO: Casteo a ICommunicationObject para poder leer el State ---
            bool isConnected = client != null && ((ICommunicationObject)client).State == CommunicationState.Opened;
            return isConnected;
        }

        private static void SafeCloseClient()
        {
            if (client == null)
            {
                return;
            }

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

        private static void ShowConnectionError(string message)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, Lang.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
    }
}