import uuid
import streamlit as st
import api_client
import auth
from config import USERS_CATALOG, COMPANY_A_ID, COMPANY_B_ID, BRANCH_A1_ID

st.title("5. User, Role & Organisational Scope Management")
st.caption("Inspect and manage users, roles, and functional permissions via supported security endpoints.")

user_info = auth.get_current_user()
st.info(f"**Current Actor:** `{auth.get_current_user_label()}`")

tab1, tab2, tab3, tab4 = st.tabs(["Create User", "Create Role & Permissions", "Org Scope Assignment Diagnostic", "Test User Catalog"])

with tab1:
    st.subheader("Create Tenant User via API")
    st.markdown("Endpoint: `POST /api/user` (Requires valid tenant context)")
    
    col1, col2 = st.columns(2)
    with col1:
        uname = st.text_input("Username", f"user_{uuid.uuid4().hex[:6]}")
        uemail = st.text_input("Email", f"{uname}@tenant.local")
        upassword = st.text_input("Password", "TestPassword123!", type="password")
    with col2:
        st.caption("The backend enforces USERS subscription quota checks and duplicates validation.")
        if st.button("Submit User Creation", type="primary"):
            payload = {
                "username": uname,
                "email": uemail,
                "password": upassword
            }
            code, body, _ = api_client.post("/api/user", payload)
            api_client.display_api_result(code, body)

with tab2:
    st.subheader("Create Custom Role & Assign Permission")
    st.markdown("Endpoints: `POST /api/role`, `POST /api/permission`, `POST /api/rolepermission`")
    
    col_r, col_p = st.columns(2)
    with col_r:
        st.markdown("#### Create Role")
        r_name = st.text_input("Role Name", "Senior Buyer")
        r_desc = st.text_input("Description", "Can manage purchase orders")
        if st.button("Create Role"):
            code, body, _ = api_client.post("/api/role", {"name": r_name, "description": r_desc})
            api_client.display_api_result(code, body)

    with col_p:
        st.markdown("#### Create Permission")
        p_code = st.text_input("Permission Code", "PURCHASE_REPORT.VIEW")
        p_name = st.text_input("Permission Name", "View Purchase Reports")
        p_module = st.text_input("Module", "Procurement")
        if st.button("Create Permission"):
            code, body, _ = api_client.post("/api/permission", {
                "code": p_code,
                "name": p_name,
                "description": p_name,
                "module": p_module
            })
            api_client.display_api_result(code, body)

with tab3:
    st.subheader("Organisational Scope Access Check Endpoint")
    st.markdown("Endpoint: `GET /api/user/access-test/company/{companyId}`")
    st.markdown("Validates server-side `IUserAccessService.CanAccessCompanyAsync` without executing full entity queries.")

    test_comp = st.selectbox(
        "Company to Test Access:",
        [
            f"Company A ({COMPANY_A_ID})",
            f"Company B ({COMPANY_B_ID})"
        ]
    )
    c_id = test_comp.split("(")[1].split(")")[0]

    if st.button("Run Company Access Check"):
        code, body, _ = api_client.get(f"/api/user/access-test/company/{c_id}")
        api_client.display_api_result(code, body)

with tab4:
    st.subheader("Pre-Configured Test Persona Matrix")
    st.markdown("The following users represent distinct roles and organizational scopes:")

    for label, u in USERS_CATALOG.items():
        with st.expander(f"{label} ({u['username']})"):
            st.write(f"- **User ID:** `{u['user_id']}`")
            st.write(f"- **Tenant ID:** `{u['tenant_id'] or 'Platform'}`")
            st.write(f"- **Roles:** `{u['roles']}`")
            st.write(f"- **Permissions Count:** {len(u['permissions'])}")
            st.code(", ".join(u["permissions"]), language="text")
