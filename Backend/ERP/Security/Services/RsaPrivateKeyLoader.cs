using System.Security.Cryptography;
using System.Text;

namespace Security.Services;

public class RsaPrivateKeyLoader
{
    private readonly string _privateKeyPath;
    private readonly string _publicKeyPath;

    private RSA? _rsaPrivate;
    private RSA? _rsaPublic;

    public RsaPrivateKeyLoader()
    {
        string currentDirectory = Directory.GetCurrentDirectory();

        string secureVault = Path.GetFullPath(
            Path.Combine(
                currentDirectory,
                @"..\..\..\SecureVault"));

        _privateKeyPath = Path.Combine(
            secureVault,
            "ServerVault",
            "private_key.pem");

        _publicKeyPath = Path.Combine(
            secureVault,
            "ClientVault",
            "public_key.pem");
    }

    public string GetPrivateKeyPath()
    {
        return _privateKeyPath;
    }

    public string GetPublicKeyPath()
    {
        return _publicKeyPath;
    }

    // Step 1 - Private key exists
    public bool IsPrivateKeyFound()
    {
        return File.Exists(_privateKeyPath);
    }

    // Step 2 - Private key loads
    public bool TryLoadPrivateKey()
    {
        try
        {
            if (!IsPrivateKeyFound())
            {
                return false;
            }

            string privateKeyPem =
                File.ReadAllText(_privateKeyPath);

            _rsaPrivate = RSA.Create();

            _rsaPrivate.ImportFromPem(privateKeyPem);

            return true;
        }
        catch
        {
            _rsaPrivate?.Dispose();
            _rsaPrivate = null;

            return false;
        }
    }

    // Step 3 - Private key is valid
    public bool IsPrivateKeyValid()
    {
        try
        {
            if (_rsaPrivate == null)
            {
                return false;
            }

            return _rsaPrivate.KeySize >= 4096 &&
                   _rsaPrivate.ExportParameters(true).D != null;
        }
        catch
        {
            return false;
        }
    }

    // Step 4 - Public key exists
    public bool IsPublicKeyFound()
    {
        return File.Exists(_publicKeyPath);
    }

    // Step 5 - Public key loads
    public bool TryLoadPublicKey()
    {
        try
        {
            if (!IsPublicKeyFound())
            {
                return false;
            }

            string publicKeyPem =
                File.ReadAllText(_publicKeyPath);

            _rsaPublic = RSA.Create();

            _rsaPublic.ImportFromPem(publicKeyPem);

            return true;
        }
        catch
        {
            _rsaPublic?.Dispose();
            _rsaPublic = null;

            return false;
        }
    }

    // Step 6 - Public key is valid
    public bool IsPublicKeyValid()
    {
        try
        {
            if (_rsaPublic == null)
            {
                return false;
            }

            return _rsaPublic.KeySize >= 4096 &&
                   _rsaPublic.ExportParameters(false).Modulus != null;
        }
        catch
        {
            return false;
        }
    }

    // Step 7 - Private key signs, public key verifies
    public bool VerifyPrivatePublicPair()
    {
        try
        {
            if (_rsaPrivate == null ||
                _rsaPublic == null)
            {
                return false;
            }

            byte[] challengeData =
                Encoding.UTF8.GetBytes(
                    "RsaPublicKeyValidationChallenge");

            byte[] signature =
                _rsaPrivate.SignData(
                    challengeData,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

            return _rsaPublic.VerifyData(
                challengeData,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    public RSA? GetRsa()
    {
        return _rsaPrivate;
    }

    public RSA? GetPublicRsa()
    {
        return _rsaPublic;
    }
}