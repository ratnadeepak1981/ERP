from pathlib import Path

from cryptography.hazmat.primitives import serialization


class PublicKeyValidator:

    def __init__(self, public_key_path: Path):
        self.public_key_path = public_key_path
        self.public_key = None

    def is_key_found(self) -> bool:
        return self.public_key_path.exists()

    def load_key(self):
        key_data = self.public_key_path.read_bytes()

        self.public_key = serialization.load_pem_public_key(
            key_data
        )

        return self.public_key

    def validate(self) -> dict:

        if not self.is_key_found():
            return {
                "status": "FAIL",
                "step": "RSA public key existence",
                "message": "RSA public key file not found"
            }

        try:
            self.load_key()

            key_size = self.public_key.key_size

            if key_size < 4096:
                return {
                    "status": "FAIL",
                    "step": "RSA public key validation",
                    "message": f"RSA key size is {key_size} bits"
                }

            return {
                "status": "PASS",
                "step": "RSA public key validation",
                "message": f"RSA {key_size}-bit public key loaded successfully"
            }

        except Exception as ex:
            return {
                "status": "FAIL",
                "step": "RSA public key validation",
                "message": str(ex)
            }