using System.Security.Cryptography;

namespace P2PShare.Libs.Encryption.Asymmetrical
{
    public abstract class EncryptionAsymmetrical
    {
        protected static int _dwKeySize = 2048;
        protected RSAParameters _publicKey = new();
        public RSAParameters PublicKey { get => _publicKey; }

        protected EncryptionAsymmetrical()
        {
        }

        public static int GetPublicKeyLength(out int modulusLength, out int exponentLength)
        {
            int keyLength;

            modulusLength = 0;
            exponentLength = 0;

            using (RSACryptoServiceProvider rsaCSP = new(_dwKeySize))
            {
                RSAParameters rsaParameters = rsaCSP.ExportParameters(false);

                if (rsaParameters.Modulus is null || rsaParameters.Exponent is null) throw new CryptographicException("Failed to export public key parameters.");

                modulusLength = rsaParameters.Modulus.Length;
                exponentLength = rsaParameters.Exponent.Length;
                keyLength = modulusLength + exponentLength;
            }

            return keyLength;
        }

        public static RSAParameters[] GenerateKeys()
        {
            RSAParameters[] parameters = new RSAParameters[2];

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(_dwKeySize))
            {
                // verjeny kluc
                parameters[0] = rsa.ExportParameters(false);
                // sukromny kluc
                parameters[1] = rsa.ExportParameters(true);
            }

            return parameters;
        }

        public static bool IsPublicKeyNull(RSAParameters key)
        {
            return key.Modulus is null || key.Exponent is null ? true : false;
        }
    }
}
