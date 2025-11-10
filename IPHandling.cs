using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;

namespace P2PShare.Libs
{
    public class IPHandling
    {
        public static IPAddress? GetLocalIPv4(NetworkInterface @interface)
        {
            // finds the ip address of the selected network interface
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.Name == @interface.Name && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork) return ip.Address;
                    }
                }
            }

            return null;
        }

        public static IPAddress? GetRemoteIPAddress(TcpClient client)
        {
            return ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address ?? null;
        }
    }
}
