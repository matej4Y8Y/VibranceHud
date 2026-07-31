using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibranceHud.Licensing
{
    /// <summary>The signed form of a licence: the document, plus a signature over it.</summary>
    public sealed class LicenceEnvelope
    {
        [JsonPropertyName("doc")] public string? Doc { get; set; }
        [JsonPropertyName("sig")] public string? Sig { get; set; }
    }

    /// <summary>
    /// Creates signed licences. THE PRIVATE KEY SIDE - this never runs on a customer machine.
    ///
    /// The whole reason this is a separate class from <see cref="LicenceVerifier"/> is that the
    /// app ships only the verifier and the public key. Pulling the installer apart then lets
    /// someone check licences and never create one.
    ///
    /// That is precisely what the beta got wrong: one symmetric secret did both jobs, so it had
    /// to ship, and anyone who extracted it could mint working paid keys.
    ///
    /// ECDSA P-256 rather than Ed25519 only because .NET has it built in - a native crypto
    /// library would fight the single-file self-contained publish the app relies on.
    /// </summary>
    public static class LicenceSigner
    {
        public static void CreateKeyPair(out byte[] privateKey, out byte[] publicKey)
        {
            using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            privateKey = ec.ExportECPrivateKey();
            publicKey = ec.ExportSubjectPublicKeyInfo();
        }

        public static string Sign(LicenceDocument doc, byte[] privateKey)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (privateKey == null || privateKey.Length == 0)
                throw new ArgumentException("Private key is required.", nameof(privateKey));

            var payload = Encoding.UTF8.GetBytes(doc.ToCanonicalJson());

            using var ec = ECDsa.Create();
            ec.ImportECPrivateKey(privateKey, out _);
            var signature = ec.SignData(payload, HashAlgorithmName.SHA256);

            return JsonSerializer.Serialize(new LicenceEnvelope
            {
                Doc = Convert.ToBase64String(payload),
                Sig = Convert.ToBase64String(signature),
            });
        }
    }
}
