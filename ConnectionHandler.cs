using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public abstract class ConnectionHandler : IDisposable
    {
        protected static readonly byte _encryptionDataSize = (byte)(EncryptionSymmetrical.TagSize + EncryptionSymmetrical.NonceSize);
        protected static readonly int _initialPort = 57001, _inviteBufferSize = 1024, _fileTransportBufferSize = 8192;
        protected static readonly byte[] _y = Encoding.UTF8.GetBytes("y"), _n = Encoding.UTF8.GetBytes("n");
        protected static readonly char _inviteSeparator = ':';

        protected readonly CancellationTokenSource _cancellationTokenSource = new();

        protected TcpClient? _client;
        protected NetworkStream? _netStream;

        public event EventHandler<FilePartTransportedEventArgs>? FilePartTransported;

        protected void OnFilePartTransported(byte amountOfFiles, byte currentFile, byte part, SendReceiveEnum sendReceive)
        {
            FilePartTransported?.Invoke(this, new FilePartTransportedEventArgs(amountOfFiles, currentFile, part, sendReceive));
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            DisposeClient();
        }

        protected void DisposeClient()
        {
            _netStream?.Dispose();
            _client?.Dispose();
        }

        protected bool IsPortAvailable(IPAddress ip, byte port)
        {
            TcpListener? listener = null;

            try
            {
                listener = new(ip, port);

                listener.Start();

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                listener?.Stop();
                listener?.Dispose();
            }
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
            Dispose();
        }
    }
}
