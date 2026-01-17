using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionReceiverHandler : ConnectionHandler
    {
        public static string InviteErrorMessage { get; } = "Receiving invite failed.";

        private EncryptorAsymmetrical? _encryptor;
        private EncryptionSymmetrical? _encryptionSymmetrical;
        private Dictionary<string, long>? _filesAndSizes;

        public IPAddress LocalIP { get; }

        public ConnectionReceiverHandler(IPAddress localIP, CancellationToken cancellationToken) : base(cancellationToken) => LocalIP = localIP;

        
    }
}
