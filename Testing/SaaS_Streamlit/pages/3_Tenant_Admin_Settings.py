import streamlit as st
import api_client
import auth

st.title("3. Tenant Admin & Subscription Settings")
st.caption("Inspect authenticated tenant context and configure Tenant Procurement Approval settings.")

user_info = auth.get_current_user()
st.info(f"**Current Actor:** `{auth.get_current_user_label()}` | **Tenant ID:** `{user_info.get('tenant_id') or 'None'}`")

tab1, tab2 = st.tabs(["Tenant Context & Permissions", "Procurement Approval Settings"])

with tab1:
    st.subheader("Tenant Authenticated Context")
    st.markdown("Invokes `/api/user/context` to verify server-side claims resolution.")
    if st.button("Fetch Current User Context"):
        code, body, _ = api_client.get("/api/user/context")
        api_client.display_api_result(code, body, expected_status=200)

with tab2:
    st.subheader("Tenant Procurement Approval Settings")
    st.markdown("""
    Endpoints:
    - `GET /api/tenants/current/settings/procurement-approval` (Requires `PROCUREMENT.SETTINGS.VIEW`)
    - `PUT /api/tenants/current/settings/procurement-approval` (Requires `PROCUREMENT.SETTINGS.EDIT`)
    """)

    col_view, col_edit = st.columns(2)
    with col_view:
        st.markdown("#### View Current Settings")
        if st.button("Get Procurement Settings", type="primary"):
            code, body, _ = api_client.get("/api/tenants/current/settings/procurement-approval")
            has_view = "PROCUREMENT.SETTINGS.VIEW" in user_info.get("permissions", [])
            expected = 200 if has_view else 403
            api_client.display_api_result(code, body, expected_status=expected)

    with col_edit:
        st.markdown("#### Update Settings")
        app_req = st.checkbox("Procurement Approval Required", value=True)
        mode_choice = st.selectbox(
            "Approval Mode",
            ["Simple Approval (Mode = 1)", "Advanced Approval (Mode = 2)"],
            index=0
        )
        app_mode = 1 if "Simple" in mode_choice else 2

        if st.button("Save Procurement Settings"):
            payload = {
                "approvalRequired": app_req,
                "approvalMode": app_mode
            }
            code, body, _ = api_client.put("/api/tenants/current/settings/procurement-approval", payload)
            has_edit = "PROCUREMENT.SETTINGS.EDIT" in user_info.get("permissions", [])
            
            # Note: Advanced mode (mode 2) must be rejected with 400 Bad Request by the backend!
            if not has_edit:
                expected = 403
            elif app_mode == 2:
                expected = 400
            else:
                expected = 200
            
            api_client.display_api_result(code, body, expected_status=expected)
            if app_mode == 2 and code == 400:
                st.info("Verified: Backend rejected unsupported Advanced Approval Mode (Mode 2) as expected.")
