using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;

namespace P2PShare.Libs
{
    public class IPHandling
    {
        public static IPAddress? GetLocalIPv4(NetworkInterface @interface)
        {
            foreach (var ip in @interface.GetIPProperties().UnicastAddresses) if (ip.Address.AddressFamily == AddressFamily.InterNetwork) return ip.Address;
            return null;
        }

        public static IPAddress? GetRemoteIPAddress(TcpClient client)
        {
            return ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address ?? null;
        }
    }
}
