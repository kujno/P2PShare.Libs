using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionReceiverHandler : ConnectionHandler
    {
        private EncryptorAsymmetrical? _encryptor;
        private EncryptionSymmetrical? _encryptionSymmetrical;
        private Dictionary<string, long>? _filesAndSizes;
        private bool _encrypted;

        public IPAddress LocalIP { get; }

        public ConnectionReceiverHandler(IPAddress localIP) => LocalIP = localIP;

        public async Task<Dictionary<string, long>> ReceiveInviteAsync() // should also return if its encrypted
        {
            int modulusLength, exponentLength, read;
            var files = String.Empty;
            string[] filesSplit;
            List<byte> invite = new();

            _encryptionSymmetrical = new();

            try
            {
                byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength], encryptionBuffer = new byte[_y.Length], inviteArr;

                await ReceiveTcpClientAsync(LocalIP, _initialPort);

                _netStream = _client!.GetStream();

                await _netStream.ReadExactlyAsync(encryptionBuffer, _cancellationTokenSource.Token);
                _encrypted = encryptionBuffer == _y;

                if (_encrypted)
                {
                    await _netStream.ReadExactlyAsync(rsaKey, _cancellationTokenSource.Token);

                    Array.Copy(rsaKey, 0, modulus, 0, modulusLength);
                    Array.Copy(rsaKey, modulusLength, exponent, 0, exponentLength);

                    _encryptor = new(modulus, exponent);

                    await _netStream.WriteAsync(_encryptor.Encrypt(_encryptionSymmetrical.Key), _cancellationTokenSource.Token);
                }

                _filesAndSizes = [];

                do
                {
                    byte[] buffer = new byte[_inviteBufferSize];

                    read = await _netStream!.ReadAsync(buffer, _cancellationTokenSource.Token);

                    if (read > 0) invite.AddRange(buffer);
                }
                while (read > 0);
                inviteArr = invite.ToArray();
                files = Encoding.UTF8.GetString(_encrypted ? _encryptionSymmetrical.Decrypt(inviteArr) : inviteArr);
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch
            {
                throw new Exception("Receiving invite failed.");
            }

            filesSplit = files.Split(FileSeparator);
            foreach (var file in filesSplit)
            {
                var index = file.IndexOf(_inviteSeparator);

                _filesAndSizes[file.Substring(0, index)] = long.Parse(file.Substring(index + 1));
            }

            return _filesAndSizes;
        }

        public async Task<string[]> AcceptFilesAsync(string dictionaryPath)
        {
            List<string> savedFiles = new();

            try
            {
                bool check;
                int amountOfFiles = _filesAndSizes!.Count, port;
                long bufferSize = _initialPort.ToString().Length;
                byte[] buffer;

                buffer = new byte[_encrypted ? bufferSize + _encryptionDataSize : bufferSize];

                await _netStream!.WriteAsync(_encrypted ? _encryptor?.Encrypt(_y) : _y, _cancellationTokenSource.Token);

                // receive port number
                do
                {
                    await _netStream!.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                    port = int.Parse(Encoding.UTF8.GetString(_encrypted ? _encryptionSymmetrical!.Decrypt(buffer) : buffer));

                    check = IsPortAvailable(LocalIP, port);

                    if (!check) await _netStream.WriteAsync(_encrypted ? _encryptor?.Encrypt(_n) : _n, _cancellationTokenSource.Token);
                    else await _netStream.WriteAsync(_encrypted ? _encryptor?.Encrypt(_y) : _y, _cancellationTokenSource.Token);
                }
                while (!check);

                DisposeClient();

                await ReceiveTcpClientAsync(LocalIP, port);
                _netStream = _client?.GetStream();

                for (int i = 1; i <= _filesAndSizes.Count; i++)
                {
                    var fileAndSize = _filesAndSizes.ElementAt(i - 1);
                    string file = fileAndSize.Key, path = $"{dictionaryPath}\\{file}";

                    for (int j = 0; File.Exists(path); j++)
                    {
                        file = $"{fileAndSize.Key} ({j})";
                        path = $"{dictionaryPath}\\{file}";
                    }
                    savedFiles.Add(file);

                    using (FileStream fileStream = new(path, FileMode.Create))
                    {
                        int totalBytesRead = 0;

                        while (totalBytesRead < fileAndSize.Value)
                        {
                            bufferSize = Math.Min(_fileTransportBufferSize, fileAndSize.Value - totalBytesRead);
                            buffer = new byte[_encrypted ? bufferSize + _encryptionDataSize : bufferSize];

                            await _netStream!.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                            totalBytesRead += _encrypted ? buffer.Length - _encryptionDataSize : buffer.Length;

                            OnFilePartTransported(amountOfFiles, i, CalculatePercentage(fileAndSize.Value, totalBytesRead), SendReceive.Receive);

                            await fileStream.WriteAsync(_encrypted ? _encryptionSymmetrical?.Decrypt(buffer) : buffer, _cancellationTokenSource.Token);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch
            {
                throw new Exception("Receiving file(s) failed.");
            }

            return savedFiles.ToArray();
        }

        public async Task DenyFilesAsync()
        {
            try
            {
                await _netStream!.WriteAsync(_encrypted ? _encryptor?.Encrypt(_n) : _n, _cancellationTokenSource.Token);
            }
            catch
            {
            }
        }

        private async Task ReceiveTcpClientAsync(IPAddress ip, int port)
        {
            TcpListener? listener = null;
            TcpClient? client = null;

            try
            {
                listener = new TcpListener(ip, port);
                listener.Start();
                do
                {
                    client = await listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                }
                while (!client.Connected);
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                listener?.Stop();
                listener?.Dispose();
            }

            _client = client;
        }
    }
}
