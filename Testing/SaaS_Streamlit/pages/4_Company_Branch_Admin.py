import streamlit as st
import api_client
import auth
from config import (
    TENANT_ALPHA_ID, TENANT_BETA_ID,
    COMPANY_A_ID, COMPANY_B_ID, COMPANY_C_ID, COMPANY_D_ID,
    BRANCH_A1_ID, BRANCH_A2_ID, BRANCH_B1_ID, BRANCH_B2_ID, BRANCH_C1_ID, BRANCH_D1_ID
)

st.title("4. Company & Branch Administration")
st.caption("Inspect and validate company and branch structures across tenants and verify dual-layer access enforcement.")

user_info = auth.get_current_user()
st.info(f"**Current Actor:** `{auth.get_current_user_label()}` | **Tenant ID:** `{user_info.get('tenant_id') or 'None'}`")

tab1, tab2, tab3 = st.tabs(["List Accessible Companies", "List Accessible Branches", "Org Structure Diagnostic"])

with tab1:
    st.subheader("Query Accessible Companies")
    st.markdown("Endpoint: `GET /api/companies` (Requires `COMPANY.VIEW` and User Company Scope)")
    
    col_all, col_single = st.columns([1, 1])
    with col_all:
        if st.button("Get All Accessible Companies"):
            code, body, _ = api_client.get("/api/companies")
            api_client.display_api_result(code, body)

    with col_single:
        company_to_get = st.selectbox(
            "Select Company ID to Query Directly (`GET /api/companies/{id}`):",
            [
                f"Company A ({COMPANY_A_ID}) - Tenant Alpha",
                f"Company B ({COMPANY_B_ID}) - Tenant Alpha",
                f"Company C ({COMPANY_C_ID}) - Tenant Beta",
                f"Company D ({COMPANY_D_ID}) - Tenant Beta",
            ]
        )
        comp_id = company_to_get.split("(")[1].split(")")[0]
        if st.button("Query Specific Company"):
            code, body, _ = api_client.get(f"/api/companies/{comp_id}")
            api_client.display_api_result(code, body)

with tab2:
    st.subheader("Query Accessible Branches")
    st.markdown("Endpoint: `GET /api/companies/{companyId}/branches` and `GET /api/companies/{companyId}/branches/{branchId}`")
    
    selected_comp = st.selectbox(
        "Company for Branch Query:",
        [
            f"Company A ({COMPANY_A_ID})",
            f"Company B ({COMPANY_B_ID})",
            f"Company C ({COMPANY_C_ID})",
            f"Company D ({COMPANY_D_ID})"
        ]
    )
    c_id = selected_comp.split("(")[1].split(")")[0]

    col_b_all, col_b_one = st.columns(2)
    with col_b_all:
        if st.button("List Branches for Company"):
            code, body, _ = api_client.get(f"/api/companies/{c_id}/branches")
            api_client.display_api_result(code, body)

    with col_b_one:
        branch_options = {
            "Branch A1 (under Company A)": (COMPANY_A_ID, BRANCH_A1_ID),
            "Branch A2 (under Company A)": (COMPANY_A_ID, BRANCH_A2_ID),
            "Branch B1 (under Company B)": (COMPANY_B_ID, BRANCH_B1_ID),
            "Branch B2 (under Company B)": (COMPANY_B_ID, BRANCH_B2_ID),
            "Branch C1 (under Company C)": (COMPANY_C_ID, BRANCH_C1_ID),
            "Branch D1 (under Company D)": (COMPANY_D_ID, BRANCH_D1_ID)
        }
        b_choice = st.selectbox("Direct Branch Lookup:", list(branch_options.keys()))
        b_comp_id, b_id = branch_options[b_choice]
        if st.button("Get Branch by ID"):
            code, body, _ = api_client.get(f"/api/companies/{b_comp_id}/branches/{b_id}")
            api_client.display_api_result(code, body)

with tab3:
    st.subheader("Configured 6-Structure Test Topology")
    st.markdown("""
    The system supports **six company/branch structures** overall across two isolated tenants:

    | Tenant | Company | Branches | Hierarchy Role |
    | :--- | :--- | :--- | :--- |
    | **Tenant Alpha** (`11111111-...`) | **Company A** (`aaaaaaaa-...`) | Branch A1 (`a1a1a1a1-...`), Branch A2 (`a2a2a2a2-...`) | Multi-Branch Company (supports Branch vs Company GM testing) |
    | **Tenant Alpha** (`11111111-...`) | **Company B** (`bbbbbbbb-...`) | Branch B1 (`b1b1b1b1-...`), Branch B2 (`b2b2b2b2-...`) | Multi-Branch Sibling Company (supports Cross-Company GM testing) |
    | **Tenant Beta** (`22222222-...`) | **Company C** (`cccccccc-...`) | Branch C1 (`c1c1c1c1-...`) | Foreign Tenant Company 1 (strict cross-tenant isolation) |
    | **Tenant Beta** (`22222222-...`) | **Company D** (`dddddddd-...`) | Branch D1 (`d1d1d1d1-...`) | Foreign Tenant Company 2 (tenant isolation & scope validation) |
    """)
    st.info("Backend Note: Company/Branch records are immutable master structures maintained by database seeding/provisioning. The backend currently exposes `GET /api/companies` and `GET /api/companies/{id}/branches` query contracts with dual-layer security enforcement.")
