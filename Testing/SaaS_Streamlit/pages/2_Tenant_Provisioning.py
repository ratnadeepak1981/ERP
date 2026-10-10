import uuid
import streamlit as st
import api_client
import auth
from config import TENANT_ALPHA_ID, TENANT_BETA_ID

st.title("2. Tenant Registration & Provisioning")
st.caption("Register new SaaS tenants, execute database provisioning pipelines, and inspect tenant subscription status.")

user_info = auth.get_current_user()
st.info(f"**Current Actor:** `{auth.get_current_user_label()}`")

tab1, tab2, tab3 = st.tabs(["Tenant Registration", "Tenant Provisioning", "Verify Test Tenants"])

with tab1:
    st.subheader("Register Tenant via API")
    st.markdown("Endpoint: `POST /api/tenant/tenant-registration-test` (or `POST /api/saas/tenant-registration-test`)")
    
    col1, col2 = st.columns(2)
    with col1:
        tenant_name = st.text_input("Tenant Name", "Alpha Manufacturing Corp")
        tenant_code = st.text_input("Tenant Code", f"ALPHA_{uuid.uuid4().hex[:4].upper()}")
        storage_mode = st.selectbox("Storage Mode", ["Shared", "Dedicated"], index=0)
    with col2:
        plan_id_input = st.text_input("Subscription Plan ID (optional)", "")
        reg_btn = st.button("Register Tenant", type="primary")

    if reg_btn:
        payload = {
            "name": tenant_name,
            "code": tenant_code,
            "storageMode": storage_mode
        }
        if plan_id_input.strip():
            payload["subscriptionPlanId"] = plan_id_input.strip()

        code, body, _ = api_client.post("/api/tenant/tenant-registration-test", payload)
        api_client.display_api_result(code, body, expected_status=200)

with tab2:
    st.subheader("Tenant Provisioning")
    st.markdown("Endpoint: `POST /api/tenant/provision`")
    prov_tenant_id = st.text_input("Tenant ID to Provision", TENANT_ALPHA_ID)
    prov_storage_mode = st.selectbox("Provision Storage Mode", ["Shared", "Dedicated"], key="prov_sm")
    prov_dedicated_str = st.text_input("Dedicated Connection String (if Dedicated)", "")

    if st.button("Execute Provisioning"):
        payload = {
            "tenantId": prov_tenant_id,
            "storageMode": prov_storage_mode,
            "dedicatedConnectionString": prov_dedicated_str if prov_storage_mode == "Dedicated" else None
        }
        code, body, _ = api_client.post("/api/tenant/provision", payload)
        api_client.display_api_result(code, body)

with tab3:
    st.subheader("Designated Isolated Test Tenants Verification")
    st.markdown("The designated test environment establishes two isolated tenants:")
    col_a, col_b = st.columns(2)
    with col_a:
        st.markdown(f"""
        **Tenant A (Tenant Alpha)**
        - **Tenant ID:** `{TENANT_ALPHA_ID}`
        - **Code:** `ALPHA`
        - **Admin User:** `tenantadmin_a` (`admin_a@tenant.local`)
        - **Companies:** Company A, Company B
        """)
    with col_b:
        st.markdown(f"""
        **Tenant B (Tenant Beta)**
        - **Tenant ID:** `{TENANT_BETA_ID}`
        - **Code:** `BETA`
        - **Admin User:** `tenantadmin_b` (`admin_b@tenant.local`)
        - **Companies:** Company C, Company D
        """)
