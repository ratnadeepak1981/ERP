import streamlit as st


st.title("User & Role Management")

st.subheader("Create User")

username = st.text_input("Username")
email = st.text_input("Email")
password = st.text_input(
    "Password",
    type="password"
)

st.subheader("Assign Role")

role = st.selectbox(
    "Role",
    [
        "Tenant Admin",
        "Sales User",
        "Purchase User",
        "Inventory User"
    ]
)

if st.button("Create User", type="primary"):

    if not username or not email or not password:
        st.error("Username, email and password are required.")

    else:
        st.success(
            f"User '{username}' created with role '{role}'."
        )


st.divider()

st.subheader("Role Permissions")

permissions = {
    "Company": [
        "VIEW",
        "CREATE",
        "EDIT",
        "DELETE",
        "SOFT_DELETE"
    ],
    "Branch": [
        "VIEW",
        "CREATE",
        "EDIT",
        "DELETE",
        "SOFT_DELETE"
    ],
    "Product": [
        "VIEW",
        "CREATE",
        "EDIT",
        "DELETE",
        "SOFT_DELETE"
    ],
    "Customer": [
        "VIEW",
        "CREATE",
        "EDIT",
        "DELETE",
        "SOFT_DELETE"
    ],
    "Supplier": [
        "VIEW",
        "CREATE",
        "EDIT",
        "DELETE",
        "SOFT_DELETE"
    ]
}

for module, actions in permissions.items():

    with st.expander(module, expanded=True):

        for action in actions:
            st.checkbox(
                action,
                value=(role == "Tenant Admin"),
                disabled=True,
                key=f"{module}_{action}"
            )