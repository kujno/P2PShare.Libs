using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public abstract class ConnectionHandler : IDisposable
    {
        public static event EventHandler<FilePartTransportedEventArgs>? FilePartTransported;

        public static char FileSeparator { get; } = '|';

        protected static readonly int _encryptionDataSize = EncryptionSymmetrical.TagSize + EncryptionSymmetrical.NonceSize, _initialPort = 57001, _inviteBufferSize = 1024, _fileTransportBufferSize = 8192;
        protected static readonly byte[] _y = Encoding.UTF8.GetBytes("y"), _n = Encoding.UTF8.GetBytes("n");
        protected static readonly char _inviteSeparator = ':';

        protected readonly CancellationTokenSource _cancellationTokenSource = new();

        protected TcpClient? _client;
        protected NetworkStream? _netStream;

        protected int CalculatePercentage(long fileLength, long bytesProcessed) => (int)((100 / fileLength) * bytesProcessed);

        protected void OnFilePartTransported(int amountOfFiles, int currentFile, int part, SendReceive sendReceive)
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

        protected bool IsPortAvailable(IPAddress ip, int port)
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
