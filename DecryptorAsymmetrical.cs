using System.Security.Cryptography;

namespace P2PShare.Libs
{
    public class DecryptorAsymmetrical : EncryptionAsymmetrical
    {
        private RSAParameters _privateKey;
        
        public DecryptorAsymmetrical()
        {
            RSAParameters[] keys = GenerateKeys();

            _publicKey = keys[0];
            _privateKey = keys[1];
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            byte[] decryptedData;

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(_privateKey);

                decryptedData = rsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
            }

            return decryptedData;
        }
    }
}
