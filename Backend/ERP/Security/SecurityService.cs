namespace Security.Services;

public class SecurityService
{
    private readonly RsaPrivateKeyLoader _keyLoader;

    public SecurityService(RsaPrivateKeyLoader keyLoader)
    {
        _keyLoader = keyLoader;
    }

    public string GetStatus()
    {
        // Step 1 - SecureVault path
        if (string.IsNullOrWhiteSpace(
            _keyLoader.GetPrivateKeyPath()))
        {
            return "SecureVault connection failed";
        }

        // Step 2 - Private key exists
        if (!_keyLoader.IsPrivateKeyFound())
        {
            return "RSA private key not found";
        }

        // Step 3 - Private key loads
        if (!_keyLoader.TryLoadPrivateKey())
        {
            return "RSA private key failed to load";
        }

        // Step 4 - Private key is valid
        if (!_keyLoader.IsPrivateKeyValid())
        {
            return "RSA private key is invalid";
        }

        // Step 5 - Public key exists
        if (!_keyLoader.IsPublicKeyFound())
        {
            return "RSA public key not found";
        }

        // Step 6 - Public key loads
        if (!_keyLoader.TryLoadPublicKey())
        {
            return "RSA public key failed to load";
        }

        // Step 7 - Public key is valid
        if (!_keyLoader.IsPublicKeyValid())
        {
            return "RSA public key is invalid";
        }

        // Step 8 - Private key signs / Public key verifies
        if (!_keyLoader.VerifyPrivatePublicPair())
        {
            return "RSA public key failed signature verification";
        }

        return "RSA private and public keys are valid and signature verification passed";
    }
}