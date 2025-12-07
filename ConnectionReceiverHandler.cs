using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionReceiverHandler : ConnectionHandler
    {
        private EncryptionSymmetrical? _encryptionSymmetrical;
        private Queue<KeyValuePair<string, long>>? _filesAndSizes;

        public async Task<Queue<KeyValuePair<string, long>>> ReceiveInviteAsync(IPAddress ip)
        {
            int modulusLength, exponentLength, read;
            var files = String.Empty;
            string[] filesSplit;

            _encryptionSymmetrical = new();

            try
            {
                EncryptorAsymmetrical encryptor;
                byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength], buffer = new byte[_initialPort.ToString().Length + _encryptionDataSize];
                byte port;
                bool check;

                await ReceiveTcpClientAsync(ip, (byte)_initialPort);

                _netStream = _client?.GetStream();

                await _netStream!.ReadExactlyAsync(rsaKey, _cancellationTokenSource.Token);

                Array.Copy(rsaKey, 0, modulus, 0, modulusLength);
                Array.Copy(rsaKey, modulusLength, exponent, 0, exponentLength);

                encryptor = new(modulus, exponent);

                await _netStream.WriteAsync(encryptor.Encrypt(_encryptionSymmetrical.Key), _cancellationTokenSource.Token);

                // receive port number
                do
                {
                    await _netStream!.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                    port = byte.Parse(Encoding.UTF8.GetString(_encryptionSymmetrical.Decrypt(buffer)));

                    check = IsPortAvailable(ip, port);

                    if (!check) await _netStream.WriteAsync(encryptor.Encrypt(_n), _cancellationTokenSource.Token);
                    else await _netStream.WriteAsync(encryptor.Encrypt(_y), _cancellationTokenSource.Token);
                }
                while (!check);

                DisposeClient();

                await ReceiveTcpClientAsync(ip, port);
                _netStream = _client?.GetStream();
                _filesAndSizes = [];

                do
                {
                    buffer = new byte[_inviteBufferSize];

                    read = await _netStream!.ReadAsync(buffer, _cancellationTokenSource.Token);

                    if (read > 0) files += Encoding.UTF8.GetString(_encryptionSymmetrical.Decrypt(buffer));
                }
                while (read > 0);
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch
            {
                Dispose();

                throw new Exception("Receiving invite failed.");
            }

            filesSplit = files.Split();
            foreach (var file in filesSplit)
            {
                var index = file.IndexOf(_inviteSeparator);

                _filesAndSizes.Enqueue(new(file.Substring(0, index), long.Parse(file.Substring(index + 1))));
            }

            return _filesAndSizes;
        }

        public async Task AcceptFilesAsync(string dictionaryPath)
        {
            byte amountOfFiles = (byte)_filesAndSizes!.Count;
            int fileNum = 0;

            try
            {
                await _netStream!.WriteAsync(_y, _cancellationTokenSource.Token);

                while (_filesAndSizes!.Count > 0)
                {
                    var fileAndSize = _filesAndSizes.Dequeue();

                    fileNum++;

                    using (FileStream fileStream = new($"{dictionaryPath}\\{fileAndSize.Key}", FileMode.Create))
                    {
                        int totalBytesRead = 0;

                        while (totalBytesRead < fileAndSize.Value)
                        {
                            byte[] buffer = new byte[Math.Min(_fileTransportBufferSize, fileAndSize.Value - totalBytesRead) + _encryptionDataSize];

                            await _netStream.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                            totalBytesRead += buffer.Length - _encryptionDataSize;

                            OnFilePartTransported(amountOfFiles, (byte)fileNum, (byte)((100 / fileAndSize.Value) * totalBytesRead), SendReceiveEnum.Receive);

                            await fileStream.WriteAsync(_encryptionSymmetrical?.Decrypt(buffer), _cancellationTokenSource.Token);
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
            finally
            {
                Dispose();
            }
        }

        private async Task ReceiveTcpClientAsync(IPAddress ip, byte port)
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
                client?.Dispose();

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
