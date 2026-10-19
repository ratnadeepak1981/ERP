import os
import datetime
import jwt
from cryptography.hazmat.primitives import serialization
import streamlit as st
import requests
from config import API_BASE_URL, SECURE_VAULT_KEY_PATH, USERS_CATALOG


def load_private_key():
    """Load RSA private key from ServerVault securely."""
    if not os.path.exists(SECURE_VAULT_KEY_PATH):
        raise FileNotFoundError(f"RSA Private Key not found at {SECURE_VAULT_KEY_PATH}")
    with open(SECURE_VAULT_KEY_PATH, "rb") as f:
        key_bytes = f.read()
    return serialization.load_pem_private_key(key_bytes, password=None)


def mint_token(user_id, username, tenant_id=None, roles=None, permissions=None):
    """
    Generate a real RS256 JWT identical to TokenService in C#.
    Enforces safe token handling without exposing secrets.
    """
    roles = roles or []
    permissions = permissions or []

    private_key = load_private_key()
    now = datetime.datetime.now(datetime.timezone.utc)

    payload = {
        "sub": str(user_id),
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": username,
        "iss": "CSharpAuthServer",
        "aud": "ERP",
        "exp": now + datetime.timedelta(minutes=60),
        "nbf": now
    }

    if tenant_id:
        payload["tenant_id"] = str(tenant_id)

    if roles:
        payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] = roles

    if permissions:
        payload["permission"] = permissions

    token = jwt.encode(payload, private_key, algorithm="RS256")
    return token


def login_as_user(user_label):
    """Set the authenticated session state to a catalogued user profile."""
    if user_label not in USERS_CATALOG:
        return False
    
    cfg = USERS_CATALOG[user_label]
    token = mint_token(
        user_id=cfg["user_id"],
        username=cfg["username"],
        tenant_id=cfg["tenant_id"],
        roles=cfg["roles"],
        permissions=cfg["permissions"]
    )
    
    st.session_state["access_token"] = token
    st.session_state["current_user_label"] = user_label
    st.session_state["current_user_info"] = cfg
    return True


def login_via_api(email, password):
    """Call the backend login endpoint /api/security/login."""
    url = f"{API_BASE_URL}/api/security/login"
    try:
        res = requests.post(
            url,
            json={"email": email, "password": password},
            verify=False,
            timeout=10
        )
        if res.status_code == 200:
            data = res.json()
            st.session_state["access_token"] = data.get("token")
            st.session_state["current_user_label"] = data.get("username", email)
            st.session_state["current_user_info"] = {
                "user_id": data.get("userId"),
                "username": data.get("username"),
                "tenant_id": data.get("tenantId"),
                "roles": data.get("roles", []),
                "permissions": data.get("permissions", [])
            }
            return True, "Login successful."
        else:
            return False, f"HTTP {res.status_code}: {res.text}"
    except Exception as e:
        return False, str(e)


def get_token():
    return st.session_state.get("access_token")


def get_current_user():
    return st.session_state.get("current_user_info", {})


def get_current_user_label():
    return st.session_state.get("current_user_label", "Not Logged In")


def logout():
    st.session_state.pop("access_token", None)
    st.session_state.pop("current_user_label", None)
    st.session_state.pop("current_user_info", None)