using P2PShare.Libs.Models;
using P2PShare.Models;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace P2PShare.Libs
{
    public class FileTransport
    {
        public static event EventHandler<string?>? InviteReceived;
        public static event EventHandler<FilesBeingTransportedEventArgs>? FilesBeingTransported;
        public static event EventHandler<int>? FilePartTransported;
        public static int BufferSize { get; } = 8192;
        private static int AesKeySize { get; } = 32;
        public static byte[] Ack { get; } = Encoding.UTF8.GetBytes("y");
        private static char FileLengthSymbol { get; } = '<';
        public static char FileSeparator { get; } = '|';
        public static char EncryptionSymbol { get; } = '*';

        public static async Task<bool> SendFile(TcpClient[] clients, FileInfo[] fileInfos, EncryptionEnum encryption)
        {
            int modulusLength = 0;
            int exponentLength = 0;
            int? rsaKeyLength;
            byte[] rsaKey = Array.Empty<byte>();
            EncryptorAsymmetrical encryptorAsymmetrical = new();
            byte[] modulus = Array.Empty<byte>();
            byte[] exponent = Array.Empty<byte>();

            if (encryption == EncryptionEnum.Enabled)
            {
                rsaKeyLength = EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength);
                if (rsaKeyLength is null) return false;
                rsaKey = new byte[(int)rsaKeyLength];
                modulus = new byte[modulusLength];
                exponent = new byte[exponentLength];
            }

            NetworkStream[] streams = new NetworkStream[2];
            byte[] inviteBytes = createInvite(fileInfos, encryption);
            byte[] buffer = new byte[Ack.Length];
            bool response;

            try
            {
                streams = ClientHandling.GetStreamsFromTcpClients(clients);

                await streams[1].WriteAsync(inviteBytes, 0, inviteBytes.Length);

                Task delay = Task.Delay(10000);

                if (await Task.WhenAny(ack(streams[0]), delay) == delay) return false;

                // get invite response
                response = await ack(streams[0]);

                if (!response) return false;

                if (encryption == EncryptionEnum.Enabled)
                {
                    await streams[0].ReadExactlyAsync(rsaKey, 0, rsaKey.Length);

                    Array.Copy(rsaKey, 0, modulus, 0, modulusLength);
                    Array.Copy(rsaKey, modulusLength, exponent, 0, exponentLength);
                    encryptorAsymmetrical = new(modulus, exponent);
                }

                onFilesBeingTransported(new(fileInfos, SendReceiveEnum.Send));

                foreach (FileInfo fileInfo in fileInfos)
                {
                    int bytesRead;
                    int bytesSent = 0;
                    using FileStream fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                    byte[] aesKey = new byte[AesKeySize];
                    byte[] aesKeyEncrypted;
                    byte[] buffer2;
                    EncryptionSymmetrical cryptographySymmetrical = new();

                    if (encryption == EncryptionEnum.Enabled)
                    {
                        RandomNumberGenerator.Fill(aesKey);
                        cryptographySymmetrical = new(aesKey);

                        aesKeyEncrypted = encryptorAsymmetrical.Encrypt(aesKey);

                        await streams[0].WriteAsync(aesKeyEncrypted, 0, aesKeyEncrypted.Length);
                    }

                    while (bytesSent != fileInfo.Length && (bytesRead = await fileStream.ReadAsync(buffer2 = new byte[Math.Min(BufferSize, fileInfo.Length - bytesSent)], 0, buffer2.Length)) > 0)
                    {
                        if (encryption == EncryptionEnum.Enabled) buffer2 = cryptographySymmetrical.Encrypt(buffer2);

                        await streams[0].WriteAsync(buffer2, 0, buffer2.Length);

                        bytesSent += bytesRead;
                        OnFilePartTransported(FileHandling.CalculatePercentage(fileInfo.Length, bytesSent));
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public static async Task ReceiveInvite(TcpClient?[] clients)
        {
            if (!ConnectionClient.AreClientsConnected(clients)) return;

            NetworkStream[] streams;
            byte[] buffer = new byte[1024];
            int bytesRead;

            try
            {
                streams = ClientHandling.GetStreamsFromTcpClients(clients!);

                bytesRead = await streams[1].ReadAtLeastAsync(buffer, 1);

                await SendAck(streams[0]);

                onInviteReceived(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }
            catch
            {
                onInviteReceived(null);
            }
        }

        public static async Task<FileInfo[]?> ReceiveFile(TcpClient client, string[] filePaths, int[] fileLengths, DecryptorAsymmetrical? decryptor, EncryptionEnum encryptionEnum)
        {
            NetworkStream stream;
            byte[] aesKey = new byte[AesKeySize];
            byte[] buffer = Array.Empty<byte>();

            if (encryptionEnum == EncryptionEnum.Enabled)
            {
                RSAParameters exampleKey = EncryptionAsymmetrical.GenerateKeys()[0];

                if (EncryptionAsymmetrical.IsPublicKeyNull(exampleKey)) return null;
                buffer = new byte[exampleKey.Modulus!.Length];
            }

            FileInfo[] fileInfos = new FileInfo[filePaths.Length];

            try
            {
                EncryptionSymmetrical encryption = new();


                stream = client.GetStream();

                for (int i = 0; i < fileInfos.Length; i++)
                {
                    int j = 0;

                    do
                    {
                        if (j == 1)
                        {
                            int dotIndex = filePaths[i].LastIndexOf('.');

                            filePaths[i] = filePaths[i].Substring(0, dotIndex) + $"({j})" + filePaths[i].Substring(dotIndex);
                        }
                        else if (j > 1) filePaths[i] = filePaths[i].Substring(0, filePaths[i].LastIndexOf('(') + 1) + $"{j})";

                        fileInfos[i] = new FileInfo(filePaths[i]);

                        j++;
                    }
                    while (fileInfos[i].Exists);
                }

                onFilesBeingTransported(new(fileInfos, SendReceiveEnum.Receive));

                for (int i = 0; i < fileInfos.Length; i++)
                {
                    if (encryptionEnum == EncryptionEnum.Enabled)
                    {
                        await stream.ReadExactlyAsync(buffer, 0, buffer.Length);
                        aesKey = decryptor?.Decrypt(buffer)!;
                        encryption = new(aesKey);
                    }

                    await FileHandling.CreateFile(stream, filePaths[i], fileLengths[i], encryption, encryptionEnum);
                }
            }
            catch (Exception)
            {
                return null;
            }

            return fileInfos;
        }

        public static async Task Reply(TcpClient client, bool accepted)
        {
            NetworkStream stream;
            string reply;
            byte[] replyBytes;

            try
            {
                stream = client.GetStream();

                switch (accepted)
                {
                    case true:
                        reply = "y";

                        break;
                    case false:
                        reply = "n";

                        break;
                }
                replyBytes = Encoding.UTF8.GetBytes(reply);

                await stream.WriteAsync(replyBytes, 0, replyBytes.Length);
            }
            catch
            {
            }
        }

        public static async Task SendRSAPublicKey(NetworkStream stream, RSAParameters rsaParameters)
        {
            if (EncryptionAsymmetrical.IsPublicKeyNull(rsaParameters)) return;

            byte[] buffer = rsaParameters.Modulus!.Concat(rsaParameters.Exponent!).ToArray();

            await stream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static byte[] createInvite(FileInfo[] fileInfos, EncryptionEnum encryption)
        {
            string invite = string.Empty;

            for (int i = 0; i < fileInfos.Length; i++)
            {
                invite += $"{fileInfos[i].Name} {FileLengthSymbol}{fileInfos[i].Length}B>";

                if (fileInfos.Length > 1 && i != fileInfos.Length - 1) invite += FileSeparator;
            }

            return Encoding.UTF8.GetBytes(invite + $"{EncryptionSymbol}{encryption.ToString()}");
        }

        private static void onInviteReceived(string? invite)
        {
            InviteReceived?.Invoke(null, invite);
        }

        public static int[] GetLenghtsFromFiles(string[] files)
        {
            int[] lengths = new int[files.Length];

            for (int i = 0; i < lengths.Length; i++)
            {
                int separatorIndex = files[i].IndexOf(FileLengthSymbol);
                lengths[i] = int.Parse(files[i].Substring(separatorIndex + 1, files[i].LastIndexOf('B') - separatorIndex - 1));
            }

            return lengths;
        }

        public static string[] GetNamesFromFiles(string[] files)
        {
            string[] names = new string[files.Length];

            for (int i = 0; i < names.Length; i++)
            {
                names[i] = files[i].Substring(0, files[i].IndexOf($" {FileLengthSymbol}"));
            }

            return names;
        }

        private static void onFilesBeingTransported(FilesBeingTransportedEventArgs filesBeingTransportedEventArgs)
        {
            FilesBeingTransported?.Invoke(null, filesBeingTransportedEventArgs);
        }

        public static void OnFilePartTransported(int percentage)
        {
            FilePartTransported?.Invoke(null, percentage);
        }

        private static async Task<bool> ack(NetworkStream stream)
        {
            byte[] buffer = new byte[Ack.Length];

            await stream.ReadExactlyAsync(buffer, 0, Ack.Length);

            return buffer.SequenceEqual(Ack) ? true : false;
        }

        public static async Task SendAck(NetworkStream stream, bool yN)
        {
            byte[] ackBuffer = new byte[Ack.Length];

            switch (yN)
            {
                case true:
                    ackBuffer = Ack;

                    break;

                default:
                    ackBuffer = Encoding.UTF8.GetBytes("n");

                    break;
            }

            await stream.WriteAsync(ackBuffer, 0, Ack.Length);
        }

        public static async Task SendAck(NetworkStream stream)
        {
            await SendAck(stream, true);
        }
    }
}
