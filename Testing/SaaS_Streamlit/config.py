import os
import streamlit as st
from dotenv import load_dotenv

load_dotenv()

# Base URL for the running .NET ERP Web API
API_BASE_URL = os.getenv("API_BASE_URL", "https://localhost:7202")

# RSA SecureVault path
SECURE_VAULT_KEY_PATH = os.path.abspath(
    os.path.join(os.path.dirname(__file__), "..", "..", "SecureVault", "ServerVault", "private_key.pem")
)

# Designated Test Entities Constants
TENANT_ALPHA_ID = "11111111-1111-1111-1111-111111111111"
TENANT_BETA_ID = "22222222-2222-2222-2222-222222222222"

COMPANY_A_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
COMPANY_B_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
COMPANY_C_ID = "cccccccc-cccc-cccc-cccc-cccccccccccc"
COMPANY_D_ID = "dddddddd-dddd-dddd-dddd-dddddddddddd"

BRANCH_A1_ID = "a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1"
BRANCH_A2_ID = "a2a2a2a2-a2a2-a2a2-a2a2-a2a2a2a2a2a2"
BRANCH_B1_ID = "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1"
BRANCH_B2_ID = "b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2"
BRANCH_C1_ID = "c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1"
BRANCH_D1_ID = "d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1"

PRODUCT_ALPHA_ID = "44444444-4444-4444-4444-444444444444"
PRODUCT_BETA_ID = "66666666-6666-6666-6666-666666666666"

# Pre-seeded test users
USERS_CATALOG = {
    "Platform Admin": {
        "user_id": "99999999-9999-9999-9999-999999999999",
        "username": "platformadmin",
        "tenant_id": None,
        "roles": ["Platform Admin"],
        "permissions": ["PLATFORM.TENANT.VIEW", "PLATFORM.TENANT.PROVISION", "PLATFORM.SUBSCRIPTION.MANAGE"]
    },
    "Tenant Admin (Tenant Alpha)": {
        "user_id": "10101010-1010-1010-1010-101010101010",
        "username": "tenantadmin_a",
        "tenant_id": TENANT_ALPHA_ID,
        "roles": ["Tenant Admin"],
        "permissions": [
            "COMPANY.VIEW", "BRANCH.VIEW",
            "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.EDIT", "PURCHASE_ORDER.SUBMIT", "PURCHASE_ORDER.APPROVE", "PURCHASE_ORDER.CANCEL",
            "PURCHASE_REQUISITION.VIEW", "PURCHASE_REQUISITION.CREATE", "PURCHASE_REQUISITION.EDIT", "PURCHASE_REQUISITION.SUBMIT", "PURCHASE_REQUISITION.APPROVE", "PURCHASE_REQUISITION.CANCEL",
            "PROCUREMENT.SETTINGS.VIEW", "PROCUREMENT.SETTINGS.EDIT"
        ]
    },
    "Tenant Admin (Tenant Beta)": {
        "user_id": "20202020-2020-2020-2020-202020202020",
        "username": "tenantadmin_b",
        "tenant_id": TENANT_BETA_ID,
        "roles": ["Tenant Admin"],
        "permissions": [
            "COMPANY.VIEW", "BRANCH.VIEW",
            "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.EDIT", "PURCHASE_ORDER.SUBMIT", "PURCHASE_ORDER.APPROVE", "PURCHASE_ORDER.CANCEL",
            "PURCHASE_REQUISITION.VIEW", "PURCHASE_REQUISITION.CREATE", "PURCHASE_REQUISITION.EDIT", "PURCHASE_REQUISITION.SUBMIT", "PURCHASE_REQUISITION.APPROVE", "PURCHASE_REQUISITION.CANCEL",
            "PROCUREMENT.SETTINGS.VIEW", "PROCUREMENT.SETTINGS.EDIT"
        ]
    },
    "Procurement Requester (Branch A1)": {
        "user_id": "14141414-1414-1414-1414-141414141414",
        "username": "branchuser_a1",
        "tenant_id": TENANT_ALPHA_ID,
        "roles": ["Branch User"],
        "permissions": ["COMPANY.VIEW", "BRANCH.VIEW", "PURCHASE_REQUISITION.VIEW", "PURCHASE_REQUISITION.CREATE", "PURCHASE_REQUISITION.SUBMIT"]
    },
    "Buyer / Procurement Officer (Branch A1)": {
        "user_id": "14141414-1414-1414-1414-141414141414",
        "username": "branchuser_a1",
        "tenant_id": TENANT_ALPHA_ID,
        "roles": ["Branch User"],
        "permissions": ["COMPANY.VIEW", "BRANCH.VIEW", "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.EDIT", "PURCHASE_ORDER.SUBMIT"]
    },
    "Procurement Manager (Company A, All Branches)": {
        "user_id": "13131313-1313-1313-1313-131313131313",
        "username": "compuser_a",
        "tenant_id": TENANT_ALPHA_ID,
        "roles": ["Branch User"],
        "permissions": ["COMPANY.VIEW", "BRANCH.VIEW", "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.APPROVE", "PURCHASE_REQUISITION.VIEW", "PURCHASE_REQUISITION.APPROVE"]
    },
    "Branch Manager (Branch A1 only)": {
        "user_id": "14141414-1414-1414-1414-141414141414",
        "username": "branchuser_a1",
        "tenant_id": TENANT_ALPHA_ID,
        "roles": ["Branch User"],
        "permissions": ["COMPANY.VIEW", "BRANCH.VIEW", "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_REQUISITION.VIEW", "PURCHASE_REQUISITION.CREATE"]
    },
    "Company General Manager (Company A GM)": {
        "user_id": "13131313-1313-1313-1313-131313131313",
        "username": "compuser_a",
        "tenant_id": TENANT_ALPHA_ID,
        "roles": ["Branch User"],
        "permissions": ["COMPANY.VIEW", "BRANCH.VIEW", "PURCHASE_ORDER.VIEW", "PURCHASE_REQUISITION.VIEW", "PURCHASE_ORDER.APPROVE", "PURCHASE_REQUISITION.APPROVE"]
    },
    "Cross-Company General Manager (Company A & B)": {
        "user_id": "18181818-1818-1818-1818-181818181818",
        "username": "multicomp_user_ab",
        "tenant_id": TENANT_ALPHA_ID,
        "roles": ["Branch User"],
        "permissions": ["COMPANY.VIEW", "BRANCH.VIEW", "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_REQUISITION.VIEW"]
    },
    "Unauthorised User (No Scope / No Perms)": {
        "user_id": "17171717-1717-1717-1717-171717171717",
        "username": "readonly_user",
        "tenant_id": TENANT_ALPHA_ID,
        "roles": ["Viewer"],
        "permissions": ["VIEWER.ONLY"]
    }
}