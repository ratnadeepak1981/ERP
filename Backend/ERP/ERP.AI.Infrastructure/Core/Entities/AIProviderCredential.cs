namespace ERP.AI.Infrastructure.Core.Entities;

public class AIProviderCredential : AIAuditable
{
    public Guid Id { get; private set; }

    public Guid AIProviderId { get; private set; }

    public string KeyReferenceCode { get; private set; } = string.Empty;

    public string EncryptedSecret { get; private set; } = string.Empty;

    public string EncryptionAlgorithm { get; private set; } = "RSA-4096-OAEP-SHA256";

    public int KeyVersion { get; private set; } = 1;

    public string Environment { get; private set; } = "Production";

    public bool IsActive { get; private set; } = true;

    // Concurrency Token
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public AIProvider? Provider { get; private set; }

    private AIProviderCredential() { }

    public static AIProviderCredential Create(
        Guid id,
        Guid providerId,
        string keyReferenceCode,
        string encryptedSecret,
        string encryptionAlgorithm = "RSA-4096-OAEP-SHA256",
        int keyVersion = 1,
        string environment = "Production",
        bool isActive = true)
    {
        if (providerId == Guid.Empty)
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));

        if (string.IsNullOrWhiteSpace(keyReferenceCode))
            throw new ArgumentException("Key reference code is required.", nameof(keyReferenceCode));

        if (string.IsNullOrWhiteSpace(encryptedSecret))
            throw new ArgumentException("Encrypted secret cannot be empty.", nameof(encryptedSecret));

        if (string.IsNullOrWhiteSpace(encryptionAlgorithm))
            throw new ArgumentException("Encryption algorithm is required.", nameof(encryptionAlgorithm));

        if (keyVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(keyVersion), "Key version must be greater than zero.");

        if (string.IsNullOrWhiteSpace(environment))
            throw new ArgumentException("Environment is required.", nameof(environment));

        return new AIProviderCredential
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            AIProviderId = providerId,
            KeyReferenceCode = keyReferenceCode.Trim(),
            EncryptedSecret = encryptedSecret.Trim(),
            EncryptionAlgorithm = encryptionAlgorithm.Trim(),
            KeyVersion = keyVersion,
            Environment = environment.Trim(),
            IsActive = isActive
        };
    }

    public void UpdateCredential(
        string encryptedSecret,
        string encryptionAlgorithm,
        int keyVersion,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
            throw new ArgumentException("Encrypted secret cannot be empty.", nameof(encryptedSecret));

        if (string.IsNullOrWhiteSpace(encryptionAlgorithm))
            throw new ArgumentException("Encryption algorithm is required.", nameof(encryptionAlgorithm));

        if (keyVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(keyVersion), "Key version must be greater than zero.");

        EncryptedSecret = encryptedSecret.Trim();
        EncryptionAlgorithm = encryptionAlgorithm.Trim();
        KeyVersion = keyVersion;
        IsActive = isActive;
    }
}
