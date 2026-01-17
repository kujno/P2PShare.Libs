using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionTranscieverHandler : ConnectionHandler
    {
        public static event EventHandler<IPAddress>? Contacted;

        public ConnectionTranscieverHandler(CancellationToken cancellationToken) : base(cancellationToken)
        {
        }

        private void OnContacted(IPAddress ip) => Contacted?.Invoke(this, ip);
    }
}
