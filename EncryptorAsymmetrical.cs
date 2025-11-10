using System.Security.Cryptography;

namespace P2PShare.Libs
{
    public class EncryptorAsymmetrical : EncryptionAsymmetrical
    {
        public EncryptorAsymmetrical(byte[] modulus, byte[] exponent)
        {
            _publicKey.Modulus = modulus;
            _publicKey.Exponent = exponent;
        }

        public EncryptorAsymmetrical()
        {
        }

        public byte[] Encrypt(byte[] originalData)
        {
            byte[] encryptedData;

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(PublicKey);

                encryptedData = rsa.Encrypt(originalData, RSAEncryptionPadding.OaepSHA256);
            }

            return encryptedData;
        }
    }
}
