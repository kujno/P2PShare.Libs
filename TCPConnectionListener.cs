using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace P2PShare.Libs
{
    public class TCPConnectionListener
    {
        public static async Task<TcpClient?> WaitForConnection(int port, NetworkInterface @interface, Cancellation cancellation)
        {
            IPAddress? ipLocal = IPHandling.GetLocalIPv4(@interface);

            if (ipLocal is null || cancellation.TokenSource is null) return null;

            TcpListener listener = new TcpListener(ipLocal, port);
            TcpClient client;

            try
            {
                listener.Start();
                client = await listener.AcceptTcpClientAsync(cancellation.TokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                listener.Stop();
            }

            return client;
        }

        public static async Task ListenLoop(int port, NetworkInterface @interface, Cancellation cancellation)
        {
            while (cancellation.TokenSource is not null && !cancellation.TokenSource.Token.IsCancellationRequested)
            {
                TcpClient? client = await WaitForConnection(port, @interface, cancellation);

                if (client is null) continue;

                TCPConnectionClient.OnConnected(client);

                return;
            }

            TCPConnectionClient.OnDisconnected();
        }

        public static void GetRidOfListener(ref TcpListener listener)
        {
            listener.Stop();
            listener.Server.Dispose();
        }

        public static void StartTestOfListener(TcpListener listener)
        {
            listener.Start();
            listener.Stop();
        }
    }
}
