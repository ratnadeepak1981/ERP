import streamlit as st
import auth
from config import USERS_CATALOG, API_BASE_URL
import api_client

st.set_page_config(
    page_title="Control ERP UI — SaaS & Procurement Diagnostic",
    page_icon="🏢",
    layout="wide"
)

# Sidebar Identity Context Switcher
st.sidebar.title("🔐 Authentication & Role")
current_label = auth.get_current_user_label()
st.sidebar.markdown(f"**Current Actor:** `{current_label}`")

selected_user = st.sidebar.selectbox(
    "Switch Authenticated Persona:",
    list(USERS_CATALOG.keys()),
    index=list(USERS_CATALOG.keys()).index(current_label) if current_label in USERS_CATALOG else 0
)

col_login, col_logout = st.sidebar.columns(2)
with col_login:
    if st.button("Apply User", use_container_width=True):
        auth.login_as_user(selected_user)
        st.sidebar.success(f"Active: {selected_user}")
        st.rerun()

with col_logout:
    if st.button("Logout", use_container_width=True):
        auth.logout()
        st.sidebar.info("Logged out.")
        st.rerun()

user_info = auth.get_current_user()
if user_info:
    with st.sidebar.expander("Active User Context", expanded=False):
        st.write(f"**User ID:** `{user_info.get('user_id')}`")
        st.write(f"**Tenant ID:** `{user_info.get('tenant_id') or 'Platform (None)'}`")
        st.write(f"**Roles:** {user_info.get('roles')}")
        st.write(f"**Permissions:**")
        st.caption(", ".join(user_info.get("permissions", [])))

st.sidebar.divider()
st.sidebar.markdown(f"**Target API:** `{API_BASE_URL}`")

# Header
st.title("Control ERP UI — Diagnostic & Access Validation")
st.caption("AI-Powered Multi-Tenant Manufacturing ERP SaaS — Manual Security & Workflow Test Harness")

st.markdown("""
### Purpose & Scope
This test UI executes **direct, authentic HTTP requests** against the secured .NET ERP APIs.
All security, multi-tenant isolation, RBAC checks, and organizational scope controls are evaluated server-side.
""")

col1, col2, col3 = st.columns(3)
with col1:
    st.info("#### 1. Platform & Tenant Admin\n- Tenant provisioning & registration\n- Subscription plan management\n- Platform admin operations")
with col2:
    st.info("#### 2. Org Scope & Master Data\n- Dual-layer access checks (RBAC + Scope)\n- Branch-restricted vs Company GM\n- Cross-company & cross-tenant isolation")
with col3:
    st.info("#### 3. Procurement Workflows\n- Tenant procurement approval settings\n- Simple Approval lifecycle (PR / PO)\n- Automated vs human approval & self-approval guard")

st.divider()

st.subheader("System Health & API Connectivity")
if st.button("Check Backend API Connectivity"):
    status_code, body, _ = api_client.get("/api/security/status")
    if status_code == 200:
        st.success(f"Connected to ERP Security Service! Response: {body}")
    else:
        st.error(f"Cannot connect to ERP Backend (Status: {status_code}). Detail: {body}")

    d_code, d_body, _ = api_client.get("/api/domain/status")
    if d_code == 200:
        st.success(f"Domain Service Status: {d_body}")
    else:
        st.warning(f"Domain Service returned status {d_code}: {d_body}")