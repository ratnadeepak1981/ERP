import streamlit as st
import api_client
import auth
from config import USERS_CATALOG

st.title("1. Platform Admin & Subscription Management")
st.caption("Inspect and manage Platform-level subscription plans, parameters, limits and verify role access.")

user_info = auth.get_current_user()
st.info(f"**Current Actor:** `{auth.get_current_user_label()}` | **Roles:** `{user_info.get('roles', [])}`")

tabs = st.tabs(["Platform Admin Test", "Subscription Plans", "Subscription Parameters", "Subscription Limits"])

with tabs[0]:
    st.subheader("Platform Admin Endpoint Authorization Check")
    st.markdown("Invokes `/api/security/platform-admin-test` which requires `Roles = 'Platform Admin'`.")
    
    col1, col2 = st.columns([1, 1])
    with col1:
        if st.button("Run Platform Admin Check", type="primary"):
            status_code, body, _ = api_client.get("/api/security/platform-admin-test")
            is_platform_admin = "Platform Admin" in user_info.get("roles", [])
            expected = 200 if is_platform_admin else 403
            api_client.display_api_result(status_code, body, expected_status=expected)

with tabs[1]:
    st.subheader("Subscription Plans Management")
    st.markdown("Endpoints: `GET /api/subscriptionplan`, `POST /api/subscriptionplan`, `PUT /api/subscriptionplan/{id}`")
    
    col_get, col_create = st.columns(2)
    with col_get:
        if st.button("List Subscription Plans"):
            code, body, _ = api_client.get("/api/subscriptionplan")
            api_client.display_api_result(code, body, expected_status=200)

    with col_create:
        with st.expander("Create New Subscription Plan"):
            plan_name = st.text_input("Plan Name", "Enterprise Manufacturing Plus")
            plan_code = st.text_input("Plan Code", "ENT_MFG_PLUS")
            plan_price = st.number_input("Price", value=999.0, step=50.0)
            plan_cycle = st.selectbox("Billing Cycle (1=Monthly, 2=Quarterly, 3=SemiAnnual, 4=Annual)", [1, 2, 3, 4], index=0)
            if st.button("Submit Plan Creation"):
                payload = {
                    "name": plan_name,
                    "code": plan_code,
                    "price": plan_price,
                    "billingCycle": plan_cycle
                }
                code, body, _ = api_client.post("/api/subscriptionplan", payload)
                api_client.display_api_result(code, body)

with tabs[2]:
    st.subheader("Subscription Parameters")
    st.markdown("Endpoints: `GET /api/subscriptionparameter`, `POST /api/subscriptionparameter`")
    if st.button("List Subscription Parameters"):
        code, body, _ = api_client.get("/api/subscriptionparameter")
        api_client.display_api_result(code, body, expected_status=200)

    with st.expander("Create Subscription Parameter"):
        param_key = st.text_input("Parameter Key", "STORAGE_GB")
        param_type = st.selectbox("Parameter Type (1=Fixed, 2=Usage)", [1, 2], index=0)
        if st.button("Create Parameter"):
            payload = {"parameterKey": param_key, "parameterType": param_type}
            code, body, _ = api_client.post("/api/subscriptionparameter", payload)
            api_client.display_api_result(code, body)

with tabs[3]:
    st.subheader("Subscription Limits")
    st.markdown("Endpoints: `GET /api/subscriptionlimit`")
    if st.button("List Subscription Limits"):
        code, body, _ = api_client.get("/api/subscriptionlimit")
        api_client.display_api_result(code, body, expected_status=200)
