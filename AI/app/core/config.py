import os
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[2]

SECURE_VAULT = PROJECT_ROOT.parent / "SecureVault"

PUBLIC_KEY_PATH = (
    SECURE_VAULT
    / "ClientVault"
    / "public_key.pem"
)

# Operational environment configuration (with safe local defaults)
ERP_API_BASE_URL: str = os.getenv("ERP_API_BASE_URL", "http://127.0.0.1:5000")
DEFAULT_AI_PROVIDER: str = os.getenv("DEFAULT_AI_PROVIDER", "groq")
LOG_LEVEL: str = os.getenv("LOG_LEVEL", "INFO")