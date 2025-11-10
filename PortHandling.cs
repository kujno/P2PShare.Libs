using System.Net;
using System.Net.Sockets;

namespace P2PShare.Libs
{
    public class PortHandling
    {
        public static int FindPort(IPAddress ip)
        {
            Random random = new Random();
            int port;
            bool check;

            do
            {
                check = true;
                port = random.Next(49152, 65536);

                for (int i = 0; i < 2; i++)
                {
                    if (!IsPortAvailable(ip, port + i)) check = false;
                }
            }
            while (!check);

            return port;
        }

        public static bool IsPortAvailable(IPAddress ip, int port)
        {
            TcpListener? listener = null;
            
            try
            {
                listener = new TcpListener(ip, port);

                TCPConnectionListener.StartTestOfListener(listener);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (listener is not null) TCPConnectionListener.GetRidOfListener(ref listener);
            }
        }
    }
}
