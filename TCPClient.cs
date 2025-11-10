using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace P2PShare.Libs
{
    public class TCPClient : TCPConnection
    {
        public static event EventHandler<TcpClient>? Connected;
        public static event EventHandler? Disconnected;

        public static async Task Connect(IPAddress ip, NetworkInterface @interface, Cancellation cancellation)
        {
            IPAddress? ipLocal = IPHandling.GetLocalIPv4(@interface);
            TcpClient client;
            ValueTask connecting;

            if (ipLocal is null || cancellation.TokenSource is null)
            {
                OnDisconnected();

                return;
            }

            try
            {
                for (int i = 0; i < 2; i++)
                {
                    client = new();
                    client.Client.Bind(new IPEndPoint(ipLocal, initialPort));

                    do
                    {
                        try
                        {
                            connecting = client.ConnectAsync(ip, port, cancellation.TokenSource.Token);

                            await connecting;
                        }
                        catch
                        {
                        }
                    }
                    while (!client.Connected && cancellation.TokenSource is not null);
                }
            }
            catch
            {
            }

            if (client.Connected)
            {
                OnConnected(client);

                return;
            }

            client.Dispose();

            OnDisconnected();
        }

        public static void OnConnected(TcpClient client)
        {
            Connected?.Invoke(null, client);
        }

        public static void OnDisconnected()
        {
            Disconnected?.Invoke(null, EventArgs.Empty);
        }

        public static bool AreClientsConnected(TcpClient?[] clients)
        {
            foreach (TcpClient? client in clients)
            {
                if (client is null || !client.Connected) return false;
            }

            return true;
        }

        public static void GetRidOfClients(TcpClient?[] clients)
        {
            for (int i = 0; i < clients.Length; i++)
            {
                clients[i]?.Dispose();
                clients[i] = null;
            }
        }

        public static Task[] ConnectAll(IPAddress ip, NetworkInterface @interface, int port, Cancellation cancellation)
        {
            Task[] connecting = new Task[2];

            for (int i = 0; i < 2; i++)
            {
                connecting[i] = Connect(ip, @interface, port + i, cancellation);
            }

            return connecting;
        }
    }
}