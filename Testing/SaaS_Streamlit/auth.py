import streamlit as st


def set_token(token):
    st.session_state["access_token"] = token


def get_token():
    return st.session_state.get("access_token")


def logout():
    st.session_state.pop("access_token", None)