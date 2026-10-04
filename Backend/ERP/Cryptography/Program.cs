using System.Security.Cryptography;

string secureVault = Path.GetFullPath(
    Path.Combine(
        Directory.GetCurrentDirectory(),
        @"..\..\..\SecureVault"));

string serverVault = Path.Combine(
    secureVault,
    "ServerVault");

string clientVault = Path.Combine(
    secureVault,
    "ClientVault");

string privateKeyPath = Path.Combine(
    serverVault,
    "private_key.pem");

string publicKeyPath = Path.Combine(clientVault,"public_key.pem");

// Create vault folders if they do not exist
Directory.CreateDirectory(serverVault);
Directory.CreateDirectory(clientVault);

// Check existing key pair
bool privateKeyExists = File.Exists(privateKeyPath);
bool publicKeyExists = File.Exists(publicKeyPath);

if (privateKeyExists && publicKeyExists)
{
    Console.WriteLine("RSA key pair already exists.");
    Console.WriteLine("No new key pair was generated.");
    Console.WriteLine();

    Console.WriteLine($"Private key: {privateKeyPath}");
    Console.WriteLine($"Public key : {publicKeyPath}");

    return;
}

// Prevent creation of a mismatched pair
if (privateKeyExists || publicKeyExists)
{
    Console.WriteLine("ERROR: Incomplete RSA key pair detected.");
    Console.WriteLine("Both keys must exist together.");
    Console.WriteLine();

    Console.WriteLine($"Private key exists: {privateKeyExists}");
    Console.WriteLine($"Public key exists : {publicKeyExists}");

    return;
}

// Generate RSA 4096-bit key pair
using RSA rsa = RSA.Create(4096);

string privateKeyPem = rsa.ExportRSAPrivateKeyPem();
string publicKeyPem = rsa.ExportRSAPublicKeyPem();

// Save keys
File.WriteAllText(
    privateKeyPath,
    privateKeyPem);

File.WriteAllText(
    publicKeyPath,
    publicKeyPem);

Console.WriteLine("RSA 4096-bit key pair generated successfully.");
Console.WriteLine();

Console.WriteLine($"Private key: {privateKeyPath}");
Console.WriteLine($"Public key : {publicKeyPath}");