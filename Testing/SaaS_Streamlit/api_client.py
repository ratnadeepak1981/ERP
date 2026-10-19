import json
import requests
import streamlit as st
from config import API_BASE_URL


def get_headers():
    token = st.session_state.get("access_token")
    if not token:
        return {"Content-Type": "application/json"}
    return {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json"
    }


def request(method, endpoint, json_data=None, params=None):
    """
    Execute an actual HTTP request against the .NET API.
    Returns: (status_code, response_data_or_text, raw_response)
    """
    url = f"{API_BASE_URL}{endpoint}"
    headers = get_headers()
    try:
        res = requests.request(
            method=method,
            url=url,
            headers=headers,
            json=json_data,
            params=params,
            verify=False,
            timeout=15
        )
        try:
            body = res.json()
        except Exception:
            body = res.text
        return res.status_code, body, res
    except requests.exceptions.ConnectionError:
        return 0, f"Cannot connect to ERP Backend API at {API_BASE_URL}. Ensure the API is running.", None
    except Exception as e:
        return 0, f"Request error: {str(e)}", None


def get(endpoint, params=None):
    return request("GET", endpoint, params=params)


def post(endpoint, data=None):
    return request("POST", endpoint, json_data=data)


def put(endpoint, data=None):
    return request("PUT", endpoint, json_data=data)


def delete(endpoint):
    return request("DELETE", endpoint)


def display_api_result(status_code, body, expected_status=None):
    """Render structured diagnostic output for every test attempt."""
    st.markdown("#### API Response Details")
    col1, col2 = st.columns([1, 2])
    
    with col1:
        if status_code == 200 or status_code == 201:
            st.metric("HTTP Status", f"{status_code} OK/Created")
        elif status_code == 400:
            st.metric("HTTP Status", "400 Bad Request")
        elif status_code == 401:
            st.metric("HTTP Status", "401 Unauthorized")
        elif status_code == 403:
            st.metric("HTTP Status", "403 Forbidden")
        elif status_code == 404:
            st.metric("HTTP Status", "404 Not Found")
        elif status_code == 409:
            st.metric("HTTP Status", "409 Conflict")
        else:
            st.metric("HTTP Status", f"{status_code}")

    with col2:
        if expected_status is not None:
            passed = (status_code == expected_status)
            if passed:
                st.success(f"Outcome: PASS (Status matched expected {expected_status})")
            else:
                st.error(f"Outcome: FAIL (Expected {expected_status}, got {status_code})")

    st.markdown("**Payload / Error Body:**")
    if isinstance(body, (dict, list)):
        st.json(body)
    else:
        st.code(str(body), language="text")