from fastapi import Depends
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.config import PUBLIC_KEY_PATH
from app.core.errors import AuthenticationError
from app.core.jwt_validator import JwtValidator
from app.core.logging import tenant_id_ctx
from app.core.security import PublicKeyValidator
from app.core.security_context import SecurityContext


security_scheme = HTTPBearer(auto_error=False)


def get_current_security_context(
    credentials: HTTPAuthorizationCredentials | None = Depends(
        security_scheme
    )
) -> SecurityContext:

    if not credentials or not credentials.credentials:
        raise AuthenticationError(
            message="Bearer token is missing from the Authorization header."
        )

    public_key_validator = PublicKeyValidator(
        PUBLIC_KEY_PATH
    )

    if not public_key_validator.is_key_found():
        raise AuthenticationError(
            message="Authentication service cannot locate the public key."
        )

    try:
        public_key = public_key_validator.load_key()
    except Exception as ex:
        raise AuthenticationError(
            message=f"Authentication service failed to load the public key: {str(ex)}"
        )

    jwt_validator = JwtValidator(public_key)

    result = jwt_validator.validate(
        credentials.credentials
    )

    if result["status"] != "PASS":
        raise AuthenticationError(
            message=f"JWT validation failed: {result['message']}"
        )

    context = SecurityContext.from_claims(
        result["claims"]
    )

    if context.tenant_id:
        tenant_id_ctx.set(context.tenant_id)

    return context