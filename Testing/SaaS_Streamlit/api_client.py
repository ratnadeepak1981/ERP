import requests
import streamlit as st

from config import API_BASE_URL


def get_headers():
    token = st.session_state.get("access_token")

    if not token:
        return {}

    return {
        "Authorization": f"Bearer {token}"
    }


def get(endpoint):
    return requests.get(
        f"{API_BASE_URL}{endpoint}",
        headers=get_headers(),
        verify=False
    )


def post(endpoint, data):
    return requests.post(
        f"{API_BASE_URL}{endpoint}",
        json=data,
        headers=get_headers(),
        verify=False
    )