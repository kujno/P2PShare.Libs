using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionTranscieverHandler : ConnectionHandler
    {
        public async void SendAsync(IPAddress ipRemote, IPAddress ipLocal, FileInfo[] files)
        {
            try
            {
                if (!files.All(x => x.Exists)) throw new FileNotFoundException();

                EncryptionSymmetrical encryption;
                DecryptorAsymmetrical decryptor = new();
                Random random = new();
                byte[] buffer = new byte[decryptor.PublicKey.Modulus!.Length];
                byte port;
                string invite = String.Empty;

                await ConnectAsync(ipRemote, ipLocal, (byte)_initialPort);
                _netStream = _client?.GetStream();

                // send public key
                await _netStream!.WriteAsync(decryptor.PublicKey.Modulus!.Concat(decryptor.PublicKey.Exponent!).ToArray(), 0, EncryptionAsymmetrical.GetPublicKeyLength(out _, out _), _cancellationTokenSource.Token);

                // receive aes key
                await _netStream.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                encryption = new(decryptor.Decrypt(buffer));

                do
                {
                    do
                    {
                        port = (byte)random.Next(49152, 65536);
                    }
                    while (!IsPortAvailable(ipLocal, port));

                    await _netStream.WriteAsync(encryption.Encrypt(Encoding.UTF8.GetBytes(port.ToString())), _cancellationTokenSource.Token);
                    await _netStream.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);
                }
                while (decryptor.Decrypt(buffer) != _y);

                DisposeClient();

                await ConnectAsync(ipRemote, ipLocal, port);

                foreach (var file in files)
                {
                    invite += $" {file.Name}{_inviteSeparator}{file.Length}";
                }

                await _netStream.WriteAsync(encryption.Encrypt(Encoding.UTF8.GetBytes(invite.Trim())), _cancellationTokenSource.Token);
                buffer = new byte[_y.Length + _encryptionDataSize];
                await _netStream.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                if (encryption.Decrypt(buffer) != _y) throw new FileTransportDeniedException();

                for (int i = 0; i < files.Length; i++)
                {
                    int bytesRead = 0;

                    using (FileStream fileStream = new(files[i].FullName, FileMode.Open))
                    {
                        buffer = new byte[Math.Min(_fileTransportBufferSize, files[i].Length - bytesRead)];

                        bytesRead += await fileStream.ReadAsync(buffer, _cancellationTokenSource.Token);
                        await _netStream.WriteAsync(encryption.Encrypt(buffer), _cancellationTokenSource.Token);
                        OnFilePartTransported((byte)files.Length, (byte)i, (byte)((100 / files[i].Length) * bytesRead), SendReceiveEnum.Send);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch (FileNotFoundException)
            {
                throw new FileNotFoundException("One or more files to send were not found.");
            }
            catch (FileTransportDeniedException)
            {
                throw new FileTransportDeniedException("File transport was denied.");
            }
            catch
            {
                throw new Exception("Sending file(s) failed.");
            }
            finally
            {
                Dispose();
            }
        }

        private async Task ConnectAsync(IPAddress ipRemote, IPAddress ipLocal, byte port)
        {
            TcpClient client = new();

            try
            {
                client.Client.Bind(new IPEndPoint(ipLocal, port));

                while (!client.Connected)
                {
                    await client.ConnectAsync(ipRemote, port, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            _client = client;
        }
    }
}
