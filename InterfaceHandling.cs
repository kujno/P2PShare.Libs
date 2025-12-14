using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace P2PShare.Libs
{
    public class InterfaceHandling
    {
        public static NetworkInterface[] GetUpInterfaces() // refactor
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni is not null &&
                    ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.GetIPProperties().UnicastAddresses.Count > 0)
                .ToArray();

            if (interfaces.Length == 0) throw new Exception("No network interfaces found"); // not user handled. must be fixed

            return interfaces;
        }

        public static IPAddress? GetLocalIP(NetworkInterface @interface)
        {
            foreach (var ip in @interface.GetIPProperties().UnicastAddresses) if (ip.Address.AddressFamily == AddressFamily.InterNetwork) return ip.Address;
            return null;
        }
    }
}
