from dataclasses import dataclass

import jwt
from jwt import InvalidTokenError


@dataclass(frozen=True)
class JwtValidator:

    public_key: object
    issuer: str = "CSharpAuthServer"
    audience: str = "ERP"
    algorithm: str = "RS256"

    def validate(self, token: str) -> dict:

        try:
            payload = jwt.decode(
                token,
                self.public_key,
                algorithms=[self.algorithm],
                issuer=self.issuer,
                audience=self.audience
            )

            return {
                "status": "PASS",
                "message": "RS256 JWT validation successful",
                "claims": payload
            }

        except InvalidTokenError as ex:
            return {
                "status": "FAIL",
                "message": f"JWT validation failed: {str(ex)}"
            }

        except Exception as ex:
            return {
                "status": "FAIL",
                "message": f"JWT validation error: {str(ex)}"
            }