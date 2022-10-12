using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    /// <summary>
    /// Generates and validates the authenticator hash
    /// </summary>
    class Authenticator
    {
        private readonly SHA1 _hash;

        public Authenticator()
        {
            _hash = SHA1.Create();
        }

        /// <summary>
        /// Adds an XML document to the hash to be authenticated
        /// </summary>
        /// <param name="xmlDoc">The <see cref="XmlDocument"/> to include in the authentication</param>
        public void AddToDigest(XmlDocument xmlDoc)
        {
            // Canonicalize the RST/RSTR
            var trans = new XmlDsigExcC14NTransform();
            trans.LoadInput(xmlDoc);

            // Add the canonicalised version to the hash
            using (var stream = (Stream)trans.GetOutput(typeof(Stream)))
            using (var reader = new StreamReader(stream))
            {
                var text = reader.ReadToEnd();
                stream.Position = 0;

                var buf = new byte[1024];

                while (true)
                {
                    var read = stream.Read(buf, 0, buf.Length);

                    if (read == 0)
                        break;

                    _hash.TransformBlock(buf, 0, read, buf, 0);
                }
            }
        }

        /// <summary>
        /// Checks if the provided authenticator is valid
        /// </summary>
        /// <param name="proofToken">The key issued by the server</param>
        /// <param name="actualAuthenticator">The authenticator provided by the server</param>
        public void Validate(byte[] proofToken, byte[] actualAuthenticator)
        {
            _hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            var expectedAuthenticator = CalculatePSHA1(proofToken, Encoding.UTF8.GetBytes("AUTH-HASH").Concat(_hash.Hash).ToArray(), 256);

            if (actualAuthenticator.Length != expectedAuthenticator.Length)
                throw new ApplicationException("Invalid authenticator");

            for (var i = 0; i < actualAuthenticator.Length; i++)
            {
                if (actualAuthenticator[i] != expectedAuthenticator[i])
                    throw new ApplicationException("Invalid authenticator");
            }
        }

        // https://social.microsoft.com/Forums/en-US/c485d98b-6e0b-49e7-ab34-8ecf8d694d31/signing-soap-message-request-via-adfs?forum=crmdevelopment#6cee9fa8-dc24-4524-a5a2-c3d17e05d50e
        private byte[] CalculatePSHA1(byte[] client, byte[] server, int sizeBits)
        {
            var sizeBytes = sizeBits / 8;

            using (var hash = new HMACSHA1())
            {
                hash.Key = client;
                var bufferSize = hash.HashSize / 8 + server.Length;
                var i = 0;

                byte[] b1 = server;
                byte[] b2 = new byte[bufferSize];
                byte[] temp = null;
                byte[] psha = new byte[sizeBytes];

                while (i < sizeBytes)
                {
                    hash.Initialize();
                    b1 = hash.ComputeHash(b1);
                    b1.CopyTo(b2, 0);
                    server.CopyTo(b2, hash.HashSize / 8);

                    temp = hash.ComputeHash(b2);

                    for (var j = 0; j < temp.Length; j++)
                    {
                        if (i < sizeBytes)
                        {
                            psha[i] = temp[j];
                            i++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                return psha;
            }
        }
    }
}
