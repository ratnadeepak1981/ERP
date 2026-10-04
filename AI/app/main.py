import json
import threading
import webbrowser
from contextlib import asynccontextmanager
from pathlib import Path

import uvicorn
from fastapi import Depends, FastAPI
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.auth_dependency import get_current_security_context
from app.core.config import PUBLIC_KEY_PATH
from app.core.security import PublicKeyValidator
from app.core.security_context import SecurityContext
from app.integration.handshake_service import HandshakeService


PROJECT_ROOT = Path(__file__).resolve().parent

LAUNCH_SETTINGS_PATH = (
    PROJECT_ROOT
    / "Properties"
    / "launchSettings.json"
)


def load_launch_settings() -> dict:
    with open(
        LAUNCH_SETTINGS_PATH,
        "r",
        encoding="utf-8"
    ) as file:
        settings = json.load(file)

    return settings["profiles"]["ERP-FastAPI"]


launch_settings = load_launch_settings()


@asynccontextmanager
async def lifespan(app: FastAPI):

    validator = PublicKeyValidator(PUBLIC_KEY_PATH)

    result = validator.validate()

    print()
    print("========================================")
    print("ERP FASTAPI SECURITY STARTUP TEST")
    print("========================================")
    print(f"Status  : {result['status']}")
    print(f"Step    : {result['step']}")
    print(f"Message : {result['message']}")
    print("========================================")
    print()

    yield


app = FastAPI(
    title="ERP AI Security Test",
    version="1.0.0",
    lifespan=lifespan
)


security_scheme = HTTPBearer()

handshake_service = HandshakeService.create()


@app.get("/health")
def health():
    return {
        "status": "PASS",
        "service": "FastAPI",
        "message": "FastAPI security test service is running"
    }


@app.post("/security/handshake")
def security_handshake(
    credentials: HTTPAuthorizationCredentials = Depends(
        security_scheme
    )
):
    return handshake_service.validate_token(
        credentials.credentials
    )


@app.get("/security/protected-test")
def protected_test(
    security_context: SecurityContext = Depends(
        get_current_security_context
    )
):
    return {
        "status": "PASS",
        "message": "Protected FastAPI endpoint access granted",
        "security_context": {
            "subject": security_context.subject,
            "tenant_id": security_context.tenant_id,
            "roles": security_context.roles,
            "permissions": security_context.permissions
        }
    }


def open_swagger():
    swagger_path = launch_settings["swagger"]

    url = (
        f"http://{launch_settings['host']}:"
        f"{launch_settings['port']}"
        f"{swagger_path}"
    )

    webbrowser.open(url)


if __name__ == "__main__":

    if launch_settings.get("open_browser", False):
        threading.Timer(
            1.5,
            open_swagger
        ).start()

    uvicorn.run(
        "app.main:app",
        host=launch_settings["host"],
        port=launch_settings["port"],
        reload=launch_settings.get("reload", False)
    )