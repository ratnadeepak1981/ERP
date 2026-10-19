import base64
import logging
from pathlib import Path
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import padding
from cryptography.hazmat.primitives.serialization import load_pem_public_key, load_pem_private_key

from app.core.config import SECURE_VAULT

logger = logging.getLogger(__name__)


class CredentialVaultService:
    """
    Encrypts and decrypts provider credentials using RSA-4096 OAEP with SHA-256.
    Keys are loaded from SecureVault (outside the database):
    - Encryption: Uses ClientVault/public_key.pem
    - Decryption: Uses ServerVault/private_key.pem
    """

    def __init__(
        self,
        public_key_path: Path | None = None,
        private_key_path: Path | None = None
    ):
        self.public_key_path = public_key_path or (SECURE_VAULT / "ClientVault" / "public_key.pem")
        self.private_key_path = private_key_path or (SECURE_VAULT / "ServerVault" / "private_key.pem")

    def encrypt_credential(self, raw_secret: str) -> str:
        """
        Encrypts a raw credential using RSA OAEP SHA-256.
        Returns a base64-encoded ciphertext.
        """
        if not raw_secret or not raw_secret.strip():
            raise ValueError("Raw secret cannot be empty.")

        if not self.public_key_path.exists():
            raise FileNotFoundError(f"RSA public key not found at {self.public_key_path}")

        pub_bytes = self.public_key_path.read_bytes()
        public_key = load_pem_public_key(pub_bytes)

        ciphertext = public_key.encrypt(
            raw_secret.strip().encode("utf-8"),
            padding.OAEP(
                mgf=padding.MGF1(algorithm=hashes.SHA256()),
                algorithm=hashes.SHA256(),
                label=None
            )
        )
        return base64.b64encode(ciphertext).decode("utf-8")

    def decrypt_credential(self, encrypted_b64: str) -> str:
        """
        Decrypts a base64-encoded ciphertext using RSA OAEP SHA-256.
        Returns the plaintext credential in memory.
        """
        if not encrypted_b64 or not encrypted_b64.strip():
            raise ValueError("Encrypted payload cannot be empty.")

        if not self.private_key_path.exists():
            raise FileNotFoundError(f"RSA private key not found at {self.private_key_path}")

        priv_bytes = self.private_key_path.read_bytes()
        private_key = load_pem_private_key(priv_bytes, password=None)

        ciphertext = base64.b64decode(encrypted_b64.strip())
        plaintext_bytes = private_key.decrypt(
            ciphertext,
            padding.OAEP(
                mgf=padding.MGF1(algorithm=hashes.SHA256()),
                algorithm=hashes.SHA256(),
                label=None
            )
        )
        return plaintext_bytes.decode("utf-8")
