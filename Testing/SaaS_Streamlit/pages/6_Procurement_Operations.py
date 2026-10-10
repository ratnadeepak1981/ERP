import uuid
from datetime import datetime, timezone
import streamlit as st
import api_client
import auth
from config import (
    COMPANY_A_ID, BRANCH_A1_ID, PRODUCT_ALPHA_ID,
    COMPANY_B_ID, BRANCH_B1_ID
)

st.title("6. Procurement Operations")
st.caption("Execute and validate Purchase Requisition (PR) and Purchase Order (PO) operations against real secured APIs.")

user_info = auth.get_current_user()
st.info(f"**Current Actor:** `{auth.get_current_user_label()}` | **Tenant ID:** `{user_info.get('tenant_id') or 'None'}`")

tab_pr, tab_po, tab_history = st.tabs(["Purchase Requisitions (PR)", "Purchase Orders (PO)", "Approval History"])

with tab_pr:
    st.subheader("Purchase Requisitions Workflow")
    st.markdown("Base route: `/api/companies/{companyId}/branches/{branchId}/purchase-requisitions`")

    col_pr_create, col_pr_list = st.columns(2)
    with col_pr_create:
        st.markdown("#### Create Requisition (Draft)")
        pr_num = st.text_input("Requisition Number", f"PR-{uuid.uuid4().hex[:8].upper()}")
        pr_notes = st.text_input("Notes", "Raw materials for production line 1")
        pr_qty = st.number_input("Item Quantity", min_value=1, value=10)
        pr_cost = st.number_input("Estimated Unit Cost", min_value=1.0, value=25.0)

        if st.button("Create PR Draft", type="primary"):
            payload = {
                "requisitionNumber": pr_num,
                "requiredDate": datetime.now(timezone.utc).isoformat(),
                "notes": pr_notes,
                "items": [
                    {
                        "productId": PRODUCT_ALPHA_ID,
                        "quantity": pr_qty,
                        "estimatedUnitPrice": pr_cost,
                        "remarks": "Urgent stock"
                    }
                ]
            }
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-requisitions",
                payload
            )
            api_client.display_api_result(code, body)
            if code == 201 and isinstance(body, dict):
                st.session_state["last_pr_id"] = body.get("id")

    with col_pr_list:
        st.markdown("#### Query Requisitions")
        if st.button("List PRs (Company A / Branch A1)"):
            code, body, _ = api_client.get(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-requisitions"
            )
            api_client.display_api_result(code, body)

    st.divider()
    st.markdown("#### PR Lifecycle Operations")
    target_pr_id = st.text_input("Target Requisition ID", st.session_state.get("last_pr_id", ""))
    
    col_sub, col_app, col_rej, col_can = st.columns(4)
    with col_sub:
        if st.button("Submit PR"):
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-requisitions/{target_pr_id}/submit"
            )
            api_client.display_api_result(code, body)

    with col_app:
        app_rem = st.text_input("Approve Remarks", "Approved as requested", key="pr_app_rem")
        if st.button("Approve PR"):
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-requisitions/{target_pr_id}/approve",
                {"remarks": app_rem}
            )
            api_client.display_api_result(code, body)

    with col_rej:
        rej_rem = st.text_input("Reject Remarks", "Budget exceeded", key="pr_rej_rem")
        if st.button("Reject PR"):
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-requisitions/{target_pr_id}/reject",
                {"remarks": rej_rem}
            )
            api_client.display_api_result(code, body)

    with col_can:
        can_rem = st.text_input("Cancel Remarks", "Cancelled by user", key="pr_can_rem")
        if st.button("Cancel PR"):
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-requisitions/{target_pr_id}/cancel",
                {"remarks": can_rem}
            )
            api_client.display_api_result(code, body)

with tab_po:
    st.subheader("Purchase Orders Workflow")
    st.markdown("Base route: `/api/companies/{companyId}/branches/{branchId}/purchase-orders`")

    col_po_create, col_po_list = st.columns(2)
    with col_po_create:
        st.markdown("#### Manual PO Creation (Without PR or Price Card)")
        po_num = st.text_input("PO Number", f"PO-{uuid.uuid4().hex[:8].upper()}")
        po_qty = st.number_input("PO Quantity", min_value=1, value=5, key="po_q")
        po_price = st.number_input("Unit Price", min_value=1.0, value=50.0, key="po_p")

        if st.button("Create PO (Manual)", type="primary"):
            payload = {
                "orderNumber": po_num,
                "orderDate": datetime.now(timezone.utc).isoformat(),
                "items": [
                    {
                        "productId": PRODUCT_ALPHA_ID,
                        "quantity": po_qty,
                        "unitPrice": po_price
                    }
                ]
            }
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-orders",
                payload
            )
            api_client.display_api_result(code, body)
            if code == 201 and isinstance(body, dict):
                st.session_state["last_po_id"] = body.get("id")

    with col_po_list:
        st.markdown("#### Query Purchase Orders")
        if st.button("List POs (Company A / Branch A1)"):
            code, body, _ = api_client.get(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-orders"
            )
            api_client.display_api_result(code, body)

    st.divider()
    st.markdown("#### PO Lifecycle Operations")
    target_po_id = st.text_input("Target Purchase Order ID", st.session_state.get("last_po_id", ""))

    c_sub, c_app, c_rej, c_can = st.columns(4)
    with c_sub:
        if st.button("Submit PO"):
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-orders/{target_po_id}/submit"
            )
            api_client.display_api_result(code, body)

    with c_app:
        po_app_rem = st.text_input("Approve Remarks", "Approved PO", key="po_app_rem")
        if st.button("Approve PO"):
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-orders/{target_po_id}/approve",
                {"remarks": po_app_rem}
            )
            api_client.display_api_result(code, body)

    with c_rej:
        po_rej_rem = st.text_input("Reject Remarks", "Price mismatch", key="po_rej_rem")
        if st.button("Reject PO"):
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-orders/{target_po_id}/reject",
                {"remarks": po_rej_rem}
            )
            api_client.display_api_result(code, body)

    with c_can:
        po_can_rem = st.text_input("Cancel Remarks", "Vendor out of stock", key="po_can_rem")
        if st.button("Cancel PO"):
            code, body, _ = api_client.post(
                f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/purchase-orders/{target_po_id}/cancel",
                {"remarks": po_can_rem}
            )
            api_client.display_api_result(code, body)

with tab_history:
    st.subheader("Approval Audit History & Actor Attribution")
    doc_type = st.radio("Document Type", ["Purchase Requisition", "Purchase Order"], horizontal=True)
    doc_id = st.text_input("Document ID for Audit History", target_po_id if doc_type == "Purchase Order" else target_pr_id)

    if st.button("Fetch Approval History"):
        endpoint_frag = "purchase-orders" if doc_type == "Purchase Order" else "purchase-requisitions"
        code, body, _ = api_client.get(
            f"/api/companies/{COMPANY_A_ID}/branches/{BRANCH_A1_ID}/{endpoint_frag}/{doc_id}/approval-history"
        )
        api_client.display_api_result(code, body)
