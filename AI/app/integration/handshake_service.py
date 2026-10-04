from dataclasses import dataclass

from app.core.config import PUBLIC_KEY_PATH
from app.core.jwt_validator import JwtValidator
from app.core.security import PublicKeyValidator
from app.core.security_context import SecurityContext


@dataclass(frozen=True)
class HandshakeService:

    public_key_validator: PublicKeyValidator

    @classmethod
    def create(cls):
        return cls(
            public_key_validator=PublicKeyValidator(
                PUBLIC_KEY_PATH
            )
        )

    def validate_token(self, token: str) -> dict:

        # Stage 1: RSA public key existence
        if not self.public_key_validator.is_key_found():
            return {
                "status": "FAIL",
                "stage": "RSA public key existence",
                "message": "RSA public key file not found"
            }

        # Stage 2: RSA public key loading
        try:
            public_key = self.public_key_validator.load_key()

        except Exception as ex:
            return {
                "status": "FAIL",
                "stage": "RSA public key loading",
                "message": f"RSA public key failed to load: {str(ex)}"
            }

        # Stage 3: RSA public key validation
        try:
            key_size = public_key.key_size

            if key_size < 4096:
                return {
                    "status": "FAIL",
                    "stage": "RSA public key validation",
                    "message": f"RSA key size is only {key_size} bits"
                }

        except Exception as ex:
            return {
                "status": "FAIL",
                "stage": "RSA public key validation",
                "message": f"RSA public key validation failed: {str(ex)}"
            }

        # Stage 4: JWT validation
        try:
            jwt_validator = JwtValidator(public_key)

            result = jwt_validator.validate(token)

            if result["status"] != "PASS":
                return {
                    "status": "FAIL",
                    "stage": "JWT validation",
                    "message": result["message"]
                }

        except Exception as ex:
            return {
                "status": "FAIL",
                "stage": "JWT validation",
                "message": f"JWT validation error: {str(ex)}"
            }

        # Stage 5: Security context creation
        try:
            security_context = SecurityContext.from_claims(
                result["claims"]
            )

            return {
                "status": "PASS",
                "stage": "SecurityContext creation",
                "message": (
                    "C# RS256 JWT successfully "
                    "validated by FastAPI"
                ),
                "security_context": {
                    "subject": security_context.subject,
                    "tenant_id": security_context.tenant_id,
                    "roles": security_context.roles,
                    "permissions": security_context.permissions
                }
            }

        except Exception as ex:
            return {
                "status": "FAIL",
                "stage": "SecurityContext creation",
                "message": (
                    f"SecurityContext creation failed: {str(ex)}"
                )
            }