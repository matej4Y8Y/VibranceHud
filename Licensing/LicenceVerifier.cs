using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// Checks a signed licence. THE PUBLIC KEY SIDE - this is what ships in the app.
    ///
    /// It can confirm a licence came from the holder of the private key and has not been
    /// altered. It cannot create one. Someone who pulls the app apart and extracts every byte
    /// gains the ability to verify licences, which is worth nothing to them.
    ///
    /// Every failure path returns false. Nothing throws and nothing half-succeeds: an
    /// unreadable, truncated or hand-edited licence is simply not a licence.
    /// </summary>
    public static class LicenceVerifier
    {
        public static bool TryVerify(string? envelopeJson, byte[] publicKey, out LicenceDocument? doc)
        {
            doc = null;
            if (string.IsNullOrWhiteSpace(envelopeJson)) return false;
            if (publicKey == null || publicKey.Length == 0) return false;

            try
            {
                var envelope = JsonSerializer.Deserialize<LicenceEnvelope>(envelopeJson);
                if (envelope == null) return false;
                if (string.IsNullOrEmpty(envelope.Doc) || string.IsNullOrEmpty(envelope.Sig))
                    return false;

                byte[] payload = Convert.FromBase64String(envelope.Doc);
                byte[] signature = Convert.FromBase64String(envelope.Sig);

                using var ec = ECDsa.Create();
                ec.ImportSubjectPublicKeyInfo(publicKey, out _);
                if (!ec.VerifyData(payload, signature, HashAlgorithmName.SHA256)) return false;

                // Only read the contents once the signature checks out, so nothing
                // attacker-controlled is interpreted before it has been authenticated.
                return LicenceDocument.TryFromJson(Encoding.UTF8.GetString(payload), out doc);
            }
            catch
            {
                doc = null;
                return false;
            }
        }
    }
}
