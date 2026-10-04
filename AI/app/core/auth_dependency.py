from fastapi import Depends
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.config import PUBLIC_KEY_PATH
from app.core.jwt_validator import JwtValidator
from app.core.security import PublicKeyValidator
from app.core.security_context import SecurityContext


security_scheme = HTTPBearer()


def get_current_security_context(
    credentials: HTTPAuthorizationCredentials = Depends(
        security_scheme
    )
) -> SecurityContext:

    public_key_validator = PublicKeyValidator(
        PUBLIC_KEY_PATH
    )

    if not public_key_validator.is_key_found():
        raise ValueError(
            "RSA public key file not found"
        )

    public_key = public_key_validator.load_key()

    jwt_validator = JwtValidator(public_key)

    result = jwt_validator.validate(
        credentials.credentials
    )

    if result["status"] != "PASS":
        raise ValueError(
            f"JWT validation failed: {result['message']}"
        )

    return SecurityContext.from_claims(
        result["claims"]
    )