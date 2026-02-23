using P2PShare.Libs.Encryption.Asymmetrical;
using P2PShare.Libs.Encryption.Symmetrical;
using P2PShare.Libs.Models;
using P2PShare.Libs.Models.Exceptions;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public abstract class ConnectionHandler : IDisposable
    {
        public static event EventHandler<FilePartTransportedEventArgs>? FilePartTransported;

        public static string InviteErrorMessage { get; } = "Receiving invite failed.";
        public static string CouldNotOpenFileErrorMessage { get; } = "Couldn't create file in the desired folder.";
        public static char FileSeparator { get; } = '|';
        public static char InviteSeparator { get; } = ':';
        public static int BufferSize { get; } = 8192;

        public required IPAddress IPLocal { get; init; }
        public required CancellationToken CancellationToken { get; init; }

        protected static readonly int _initialPort = 57001, _initialServerPort = _initialPort + 1;

        protected int _publicKeyLength, _modulusLength, _exponentLength, _encryptionDataSize = EncryptionSymmetrical.TagSize + EncryptionSymmetrical.NonceSize;
        protected NetworkStream? _netStream;
        protected IPAddress? _ipRemote;
        protected EncryptionSymmetrical? _encryptionSymmetrical;

        private static readonly byte[] _y = Encoding.UTF8.GetBytes("y"), _n = Encoding.UTF8.GetBytes("n");

        private TcpClient? _client;
        private DecryptorAsymmetrical? _decryptorAsymmetrical;
        private EncryptorAsymmetrical? _encryptorAsymmetrical;

        protected TcpClient Client
        {
            get => _client!;
            set
            {
                _client = value;
                _netStream = _client.GetStream();
            }
        }

        protected ConnectionHandler() => _publicKeyLength = EncryptionAsymmetrical.GetPublicKeyLength(out _modulusLength, out _exponentLength);

        protected int CalculatePercentage(long fileLength, long bytesProcessed) => (int)(100 * bytesProcessed / fileLength);

        public void Dispose() => _client?.Dispose();

        protected void OnFilePartTransported(int amountOfFiles, int currentFile, int part, SendReceive sendReceive)
        {
            FilePartTransported?.Invoke(this, new()
            {
                AmountOfFiles = amountOfFiles,
                CurrentFile = currentFile,
                Part = part,
                SendReceive = sendReceive
            });
        }

        protected bool IsPortAvailable(IPAddress ip, int port)
        {
            if (port == _initialPort || port == _initialServerPort) return false;

            try
            {
                using (TcpListener listener = new(ip, port))
                {
                    listener.Start();

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        protected async Task SendEncryptionKeyAsync()
        {
            byte[] buffer = new byte[_publicKeyLength];

            await _netStream!.ReadExactlyAsync(buffer, CancellationToken);

            _encryptorAsymmetrical = new(buffer[0.._modulusLength], buffer[_modulusLength..(_modulusLength + _exponentLength)]);

            await _netStream!.WriteAsync(_encryptorAsymmetrical!.Encrypt((_encryptionSymmetrical = new EncryptionSymmetrical()).Key), CancellationToken);
        }

        protected async Task ReceiveEncryptionKeyAsync()
        {
            byte[] buffer = new byte[_modulusLength];

            _decryptorAsymmetrical = new();

            await _netStream!.WriteAsync(_decryptorAsymmetrical.PublicKey.Modulus!.Concat(_decryptorAsymmetrical.PublicKey.Exponent!).ToArray(), CancellationToken);

            await _netStream!.ReadExactlyAsync(buffer, CancellationToken);

            _encryptionSymmetrical = new(_decryptorAsymmetrical!.Decrypt(buffer));
        }

        protected async Task<bool> SendInviteAsync(FileInfo[] files, bool encrypted)
        {
            var invite = String.Empty;

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];

                invite += $"{file.Name}{InviteSeparator}{file.Length}";
                if (i < files.Length - 1) invite += FileSeparator;
            }

            return await SendRequestYNAsync(invite.Trim(), encrypted);
        }

        public async Task SendInfoAsync(string request) => await SendInfoAsync(request, true);
        public async Task SendInfoAsync(string request, bool encrypted)
        {
            var requestBytes = Encoding.UTF8.GetBytes(request);

            if (encrypted)
                requestBytes = _encryptionSymmetrical!.Encrypt(requestBytes);

            // Poslanie dĺžky pozvánky.
            await SendInfoLengthAsync(requestBytes.Length, encrypted);

            // Odozva.
            await YNReceiveAsync(encrypted);

            // Poslanie pozvánky.
            await _netStream!.WriteAsync(requestBytes, CancellationToken);
        }

        public async Task<bool> SendRequestYNAsync(string request) => await SendRequestYNAsync(request, true);

        protected async Task<bool> SendRequestYNAsync(string request, bool encrypted)
        {
            await SendInfoAsync(request, encrypted);

            return await YNReceiveAsync(encrypted);
        }

        protected async Task SendInfoLengthAsync(int length, bool encrypted)
        {
            var lengthBytes = Encoding.UTF8.GetBytes(length.ToString());

            await _netStream!.WriteAsync(encrypted ? _encryptionSymmetrical?.Encrypt(lengthBytes) : lengthBytes, CancellationToken);
        }

        protected async Task<int> SendPortAsync(bool encrypted)
        {
            Random random = new();
            byte[] bufferPort;
            int port;

            do
            {
                do
                {
                    port = random.Next(49152, 65536);
                }
                while (!IsPortAvailable(IPLocal!, port));

                bufferPort = Encoding.UTF8.GetBytes(port.ToString());

                await _netStream!.WriteAsync(encrypted ? _encryptionSymmetrical?.Encrypt(bufferPort) : bufferPort, CancellationToken);
            }
            while (!await YNReceiveAsync(encrypted));

            return port;
        }

        protected async Task<int> ReceivePortAsync(bool encrypted)
        {
            int port;
            bool check;
            var portBytesLength = Encoding.UTF8.GetBytes(_initialPort.ToString()).Length;
            var buffer = new byte[encrypted ? portBytesLength + _encryptionDataSize : portBytesLength];

            do
            {
                await _netStream!.ReadExactlyAsync(buffer, CancellationToken);

                port = int.Parse(Encoding.UTF8.GetString(encrypted ? _encryptionSymmetrical!.Decrypt(buffer) : buffer));

                check = IsPortAvailable(IPLocal!, port);

                await YNSendAsync(encrypted, check);
            }
            while (!check);

            return port;
        }

        protected async Task SendFilesAsync(FileInfo[] files, bool encrypted)
        {
            for (int i = 0; i < files.Length; i++)
            {
                FileStream? fileStream = null;


                try
                {
                    try
                    {
                        fileStream = new(files[i].FullName, FileMode.Open);
                    }
                    catch (Exception ex)
                    {
                        throw new CouldNotOpenFileException($"Couldn't open: {files[i].Name}.", ex);
                    }

                    for (long j = 0; j < files[i].Length;)
                    {
                        byte[] buffer = new byte[Math.Min(BufferSize, files[i].Length - j)];

                        j += await fileStream.ReadAsync(buffer, CancellationToken);
                        await _netStream!.WriteAsync(encrypted ? _encryptionSymmetrical?.Encrypt(buffer) : buffer, CancellationToken);
                        OnFilePartTransported(files.Length, i + 1, CalculatePercentage(files[i].Length, j), SendReceive.Send);
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    fileStream?.Dispose();
                }
            }
        }

        public async Task YNSendAsync(bool encrypted, bool yn)
        {
            byte[] responseBuffer = yn ? _y : _n;

            await _netStream!.WriteAsync(encrypted ? _encryptionSymmetrical?.Encrypt(responseBuffer) : responseBuffer, CancellationToken);
        }

        protected async Task YNSendAsync(bool encrypted) => await YNSendAsync(encrypted, true);

        protected async Task<bool> YNReceiveAsync(bool encrypted)
        {
            var buffer = new byte[encrypted ? _y.Length + _encryptionDataSize : _y.Length];

            await _netStream!.ReadExactlyAsync(buffer, CancellationToken);

            return (encrypted ? _encryptionSymmetrical?.Decrypt(buffer) : buffer).SequenceEqual(_y);
        }

        protected async Task<T> ReceiveInviteAsync<T>(bool encrypted)
        {
            if (typeof(T) != typeof(string[]) && typeof(T) != typeof(Dictionary<string, long>)) throw new NotImplementedException();

            string[] filesSplit;
            byte[] buffer = new byte[BufferSize];
            Dictionary<string, long> filesAndSizes;

            try
            {
                filesSplit = (await ReceiveInfoAsync(encrypted)).Split(FileSeparator);
            }
            catch (Exception ex)
            {
                throw new FormatException(InviteErrorMessage, ex);
            }

            if (typeof(T) == typeof(string[])) return (T)(object)filesSplit;

            filesAndSizes = [];
            foreach (var file in filesSplit)
            {
                var index = file.IndexOf(InviteSeparator);

                filesAndSizes[file.Substring(0, index)] = long.Parse(file.Substring(index + 1));
            }

            return (T)(object)filesAndSizes;
        }

        public async Task<string> ReceiveInfoAsync() => await ReceiveInfoAsync(true);
        public async Task<string> ReceiveInfoAsync(bool encrypted)
        {
            int inviteLength;
            var buffer = new byte[BufferSize];
            // Prijatie dĺžky žiadosti.
            var read = await _netStream!.ReadAsync(buffer, CancellationToken);

            // Poslatie odozvy.
            await YNSendAsync(encrypted);

            buffer = buffer[0..read];
            if (encrypted) buffer = _encryptionSymmetrical!.Decrypt(buffer);
            if (!int.TryParse(Encoding.UTF8.GetString(buffer), out inviteLength)) throw new FormatException();
            // prijatie ziadosti
            await _netStream!.ReadExactlyAsync(buffer = new byte[inviteLength], CancellationToken);

            return Encoding.UTF8.GetString(encrypted ? _encryptionSymmetrical!.Decrypt(buffer) : buffer);
        }

        public async Task<string[]> ReceiveFilesAsync(Dictionary<string, long> filesAndSizes, string dictionaryPath, bool encrypted)
        {
            List<string> savedFiles = new();
            FileStream? fileStream = null;

            try
            {
                for (int i = 1; i <= filesAndSizes.Count; i++)
                {
                    var fileAndSize = filesAndSizes.ElementAt(i - 1);
                    var dotIndex = fileAndSize.Key.LastIndexOf('.');
                    long totalBytesRead = 0;
                    string fileName = fileAndSize.Key.Substring(0, dotIndex), fileExt = fileAndSize.Key.Substring(dotIndex + 1), file = $"{fileName}.{fileExt}", path = $"{dictionaryPath}\\{file}";

                    for (int j = 0; File.Exists(path); j++)
                    {
                        file = $"{fileName} ({j}).{fileExt}";
                        path = $"{dictionaryPath}\\{file}";
                    }

                    try
                    {
                        fileStream = new(path, FileMode.Create);
                    }
                    catch (Exception ex)
                    {
                        throw new CouldNotOpenFileException(CouldNotOpenFileErrorMessage, ex);
                    }

                    while (totalBytesRead < fileAndSize.Value)
                    {
                        var bufferSize = Math.Min(BufferSize, fileAndSize.Value - totalBytesRead);
                        var buffer = new byte[encrypted ? bufferSize + _encryptionDataSize : bufferSize];

                        await _netStream!.ReadExactlyAsync(buffer, CancellationToken);

                        totalBytesRead += encrypted ? buffer.Length - _encryptionDataSize : buffer.Length;

                        OnFilePartTransported(filesAndSizes.Count, i, CalculatePercentage(fileAndSize.Value, totalBytesRead), SendReceive.Receive);

                        await fileStream.WriteAsync(encrypted ? _encryptionSymmetrical?.Decrypt(buffer) : buffer, CancellationToken);
                    }

                    savedFiles.Add(file);
                }
            }
            finally
            {
                fileStream?.Dispose();
            }

            return savedFiles.ToArray();
        }

        protected async Task<TcpClient> ReceiveTcpClientAsync(int port)
        {
            using (TcpListener listener = new(IPLocal!, port))
            {
                listener.Start();

                return await listener.AcceptTcpClientAsync(CancellationToken);
            }
        }

        protected async Task<TcpClient> ConnectAsync(int port, bool connectingToServer)
        {
            TcpClient client = new();

            try
            {
                bool connected;

                client.Client.Bind(new IPEndPoint(IPLocal!, 0));

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken))
                {
                    using (var timer = connectingToServer ? Task.Run(async () => await Task.Delay(20000), CancellationToken) : null)
                    {
                        do
                        {
                            try
                            {
                                await client.ConnectAsync(_ipRemote!, port, CancellationToken);

                                connected = client.Connected;
                            }
                            catch
                            {
                                connected = false;
                            }
                        }
                        while (!connected && ((!timer?.IsCompleted) ?? true) && !CancellationToken.IsCancellationRequested);
                    }

                    cts.Cancel();
                }
            }
            catch (Exception ex)
            {
                client.Dispose();
                throw new ConnectionFailedException("Could not connect.", ex);
            }

            return client;
        }
    }
}
