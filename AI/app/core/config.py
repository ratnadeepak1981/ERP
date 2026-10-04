from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[2]

SECURE_VAULT = PROJECT_ROOT.parent / "SecureVault"

PUBLIC_KEY_PATH = (
    SECURE_VAULT
    / "ClientVault"
    / "public_key.pem"
)