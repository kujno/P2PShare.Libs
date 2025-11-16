using System.Net.NetworkInformation;

namespace P2PShare.Libs
{
    public class InterfaceHandling
    {
        public static event EventHandler? InterfaceDown;

        public static List<NetworkInterface> GetUpInterfaces()
        {
            List<NetworkInterface> interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni is not null).Cast<NetworkInterface>().ToList();
            
            if (interfaces.Count.Equals(0)) throw new Exception("No network interfaces found"); // not user handled. must be fixed

            foreach (NetworkInterface ni in interfaces)
            {
                if (
                    ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.GetIPProperties().UnicastAddresses.Count == 0
                ) interfaces.Remove(ni);
            }

            return interfaces;
        }

        public static async Task MonitorInterface(NetworkInterface @interface, Cancellation cancellation)
        {
            try
            {
                while (@interface.OperationalStatus == OperationalStatus.Up)
                {
                    if (cancellation.TokenSource is null || cancellation.TokenSource.Token.IsCancellationRequested) return;

                    await Task.Delay(1000);
                }
            }
            catch
            {

            }

            OnInterfaceDown();
        }

        private static void OnInterfaceDown()
        {
            InterfaceDown?.Invoke(null, EventArgs.Empty);
        }
    }
}
