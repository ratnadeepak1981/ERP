import os
import requests
import streamlit as st

# Configuration & FastAPI base URL
DEFAULT_FASTAPI_URL = os.getenv("FASTAPI_BASE_URL", "http://127.0.0.1:8000")

st.set_page_config(
    page_title="AI Control Center",
    page_icon="🤖",
    layout="wide",
    initial_sidebar_state="expanded"
)

# Sidebar settings & navigation
st.sidebar.title("🤖 AI Control Center")
st.sidebar.caption("Manufacturing ERP SaaS AI Platform")

fastapi_url = st.sidebar.text_input(
    "FastAPI Service URL",
    value=DEFAULT_FASTAPI_URL,
    help="Target FastAPI service URL."
)

section = st.sidebar.radio(
    "Navigation",
    ["📊 Dashboard", "⚙️ Provider Configuration", "🔍 Diagnostics", "💾 Saved Configuration"]
)

# Helper function to call backend
def call_api(method: str, endpoint: str, json_data: dict = None):
    url = f"{fastapi_url.rstrip('/')}/{endpoint.lstrip('/')}"
    try:
        if method == "GET":
            resp = requests.get(url, timeout=10)
        elif method == "POST":
            resp = requests.post(url, json=json_data, timeout=30)
        return resp.status_code, resp.json()
    except requests.exceptions.ConnectionError:
        return 0, {"error": f"Cannot connect to FastAPI service at {fastapi_url}. Please ensure the service is running."}
    except Exception as ex:
        return 0, {"error": str(ex)}


# =========================================================
# SECTION A: DASHBOARD
# =========================================================
if section == "📊 Dashboard":
    st.header("AI Platform Overview")
    st.markdown("Monitor connection health, model catalog readiness, and diagnostic summaries across providers.")

    code, health_data = call_api("GET", "/health")
    if code == 200 and health_data.get("status") == "PASS":
        st.success("🟢 **FastAPI Service**: Online & Healthy")
    else:
        st.error(f"🔴 **FastAPI Service**: Unreachable or Degraded (Status: {code})")
        if "error" in health_data:
            st.warning(health_data["error"])

    st.divider()

    # Load provider statuses
    status_code, providers = call_api("GET", "/control-center/status")

    col1, col2 = st.columns(2)

    with col1:
        st.subheader("Groq Provider Status")
        groq_info = providers.get("groq", {}) if status_code == 200 else {}
        is_cfg = groq_info.get("is_configured", False)
        tst_status = groq_info.get("last_test_status", "NOT_TESTED")
        has_enc = groq_info.get("has_encrypted_credential", False)

        st.metric("Configuration Status", "Configured (Encrypted)" if has_enc else ("Configured" if is_cfg else "Unconfigured"))
        st.metric("Test Status", tst_status)
        st.write(f"**Default Model:** `{groq_info.get('default_model') or 'None'}`")
        st.write(f"**Discovered Models:** {len(groq_info.get('available_models', []))}")

    with col2:
        st.subheader("Google Gemini Status")
        gemini_info = providers.get("gemini", {}) if status_code == 200 else {}
        is_cfg = gemini_info.get("is_configured", False)
        tst_status = gemini_info.get("last_test_status", "NOT_TESTED")
        has_enc = gemini_info.get("has_encrypted_credential", False)

        st.metric("Configuration Status", "Configured (Encrypted)" if has_enc else ("Configured" if is_cfg else "Unconfigured"))
        st.metric("Test Status", tst_status)
        st.write(f"**Default Model:** `{gemini_info.get('default_model') or 'None'}`")
        st.write(f"**Discovered Models:** {len(gemini_info.get('available_models', []))}")


# =========================================================
# SECTION B: PROVIDER CONFIGURATION
# =========================================================
elif section == "⚙️ Provider Configuration":
    st.header("Provider & Model Configuration")
    st.markdown(
        "Follow the required workflow for each provider:\n"
        "1. Enter API Key in the masked field (or use existing encrypted credential).\n"
        "2. Click **Test Connection** to verify live authentication.\n"
        "3. Once verified, click **Load Models** to dynamically discover models.\n"
        "4. Select a default model.\n"
        "5. Click **Save Configuration** to encrypt the key server-side and persist settings."
    )
    st.info("🔒 **Security Note**: Raw credentials are never stored in plain text or returned to the browser. Keys are encrypted server-side using RSA-4096 OAEP SHA-256.")

    # Fetch current statuses
    _, providers = call_api("GET", "/control-center/status")

    provider_tab1, provider_tab2 = st.tabs(["⚡ Groq", "✨ Google Gemini"])

    # --- TAB 1: GROQ ---
    with provider_tab1:
        st.subheader("Groq Cloud Settings")
        groq_info = providers.get("groq", {})
        groq_has_enc = groq_info.get("has_encrypted_credential", False)

        if groq_has_enc:
            st.success("🔒 An encrypted credential is saved in secure storage. You can test connection directly without re-entering the key, or enter a new key to overwrite.")

        groq_key_input = st.text_input(
            "Groq API Key",
            type="password",
            value="",
            placeholder="gsk_... (leave empty to reuse saved encrypted credential)" if groq_has_enc else "gsk_...",
            help="Enter your Groq API key.",
            key="input_groq_key"
        )

        col_t1, col_t2 = st.columns([1, 1])

        # Step 2: Test Connection button
        with col_t1:
            btn_test_groq = st.button("🧪 Step 1: Test Connection", key="btn_test_groq")

        # Step 4: Load Models button (state-driven)
        test_passed_groq = st.session_state.get("groq_test_passed", False) or groq_info.get("last_test_status") == "SUCCESS"

        if btn_test_groq:
            payload = {"provider": "groq", "api_key": groq_key_input if groq_key_input else None}
            with st.spinner("Executing authenticated test request against Groq..."):
                c, res = call_api("POST", "/control-center/test-connection", payload)
                if res.get("success"):
                    st.success(f"✅ {res.get('message')}")
                    st.session_state["groq_test_passed"] = True
                    test_passed_groq = True
                else:
                    st.error(f"❌ Connection Failed: {res.get('message')}")
                    if res.get("error_category"):
                        st.caption(f"Error Category: `{res.get('error_category')}`")
                    # Disable downstream steps
                    st.session_state["groq_test_passed"] = False
                    st.session_state["groq_models_loaded"] = False
                    test_passed_groq = False

        with col_t2:
            btn_load_groq = st.button(
                "📥 Step 2: Load Models",
                key="btn_load_groq",
                disabled=not test_passed_groq
            )

        if btn_load_groq:
            payload = {"provider": "groq", "api_key": groq_key_input if groq_key_input else None}
            with st.spinner("Discovering models dynamically from Groq API..."):
                c, res = call_api("POST", "/control-center/load-models", payload)
                if res.get("success") and res.get("models"):
                    st.success(f"Discovered {len(res.get('models'))} models successfully!")
                    st.session_state["groq_discovered_models"] = res.get("models")
                    st.session_state["groq_models_loaded"] = True
                else:
                    st.error(f"Model Discovery Failed: {res.get('message')}")
                    st.session_state["groq_models_loaded"] = False

        # Discovered models display & selection
        discovered_groq = st.session_state.get(
            "groq_discovered_models",
            groq_info.get("available_models", [])
        )

        models_loaded_groq = st.session_state.get("groq_models_loaded", bool(discovered_groq))

        if discovered_groq:
            curr_def_groq = groq_info.get("default_model") or discovered_groq[0]
            def_idx = discovered_groq.index(curr_def_groq) if curr_def_groq in discovered_groq else 0

            selected_groq_model = st.selectbox(
                "Step 3: Select Default Model",
                options=discovered_groq,
                index=def_idx,
                key="select_groq_model"
            )
        else:
            selected_groq_model = None
            st.info("ℹ️ Models have not been loaded yet. Please complete Step 1 & Step 2 above.")

        key_ref_groq = st.text_input(
            "Key Reference Identifier (Secure reference code)",
            value=groq_info.get("key_reference_code", "GROQ_API_KEY"),
            key="groq_key_ref"
        )

        # Step 7: Save Configuration enabled only after successful test & model discovery
        can_save_groq = test_passed_groq and bool(discovered_groq) and bool(selected_groq_model)

        btn_save_groq = st.button(
            "💾 Step 4: Save Configuration",
            key="btn_save_groq",
            disabled=not can_save_groq
        )

        if btn_save_groq:
            with st.spinner("Encrypting credential server-side and persisting configuration..."):
                c, save_res = call_api("POST", "/control-center/save-configuration", {
                    "provider": "groq",
                    "default_model": selected_groq_model,
                    "key_reference_code": key_ref_groq,
                    "api_key": groq_key_input if groq_key_input else None,
                    "discovered_models": discovered_groq
                })
                if c == 200:
                    csharp_persisted = save_res.get("csharp_persisted", False)
                    if csharp_persisted:
                        st.success(f"✅ {save_res.get('message')}")
                        st.info("🔒 Credential encrypted server-side and committed centrally to AIDatabase.")
                    else:
                        st.warning(f"⚠️ {save_res.get('message')}")
                        if save_res.get("csharp_error"):
                            st.caption(f"Central Database Notice: `{save_res.get('csharp_error')}`")
                    # Clear session state input and refresh
                    st.session_state["input_groq_key"] = ""
                else:
                    st.error(f"Failed to save: {save_res}")

    # --- TAB 2: GEMINI ---
    with provider_tab2:
        st.subheader("Google Gemini Settings")
        gemini_info = providers.get("gemini", {})
        gemini_has_enc = gemini_info.get("has_encrypted_credential", False)

        if gemini_has_enc:
            st.success("🔒 An encrypted credential is saved in secure storage. You can test connection directly without re-entering the key, or enter a new key to overwrite.")

        gemini_key_input = st.text_input(
            "Google Gemini API Key",
            type="password",
            value="",
            placeholder="AIzaSy... (leave empty to reuse saved encrypted credential)" if gemini_has_enc else "AIzaSy...",
            help="Enter your Google Gemini API key.",
            key="input_gemini_key"
        )

        col_g1, col_g2 = st.columns([1, 1])

        with col_g1:
            btn_test_gemini = st.button("🧪 Step 1: Test Connection", key="btn_test_gemini")

        test_passed_gemini = st.session_state.get("gemini_test_passed", False) or gemini_info.get("last_test_status") == "SUCCESS"

        if btn_test_gemini:
            payload = {"provider": "gemini", "api_key": gemini_key_input if gemini_key_input else None}
            with st.spinner("Executing authenticated test request against Google Gemini..."):
                c, res = call_api("POST", "/control-center/test-connection", payload)
                if res.get("success"):
                    st.success(f"✅ {res.get('message')}")
                    st.session_state["gemini_test_passed"] = True
                    test_passed_gemini = True
                else:
                    st.error(f"❌ Connection Failed: {res.get('message')}")
                    if res.get("error_category"):
                        st.caption(f"Error Category: `{res.get('error_category')}`")
                    st.session_state["gemini_test_passed"] = False
                    st.session_state["gemini_models_loaded"] = False
                    test_passed_gemini = False

        with col_g2:
            btn_load_gemini = st.button(
                "📥 Step 2: Load Models",
                key="btn_load_gemini",
                disabled=not test_passed_gemini
            )

        if btn_load_gemini:
            payload = {"provider": "gemini", "api_key": gemini_key_input if gemini_key_input else None}
            with st.spinner("Discovering models dynamically from Google Gemini API..."):
                c, res = call_api("POST", "/control-center/load-models", payload)
                if res.get("success") and res.get("models"):
                    st.success(f"Discovered {len(res.get('models'))} models successfully!")
                    st.session_state["gemini_discovered_models"] = res.get("models")
                    st.session_state["gemini_models_loaded"] = True
                else:
                    st.error(f"Model Discovery Failed: {res.get('message')}")
                    st.session_state["gemini_models_loaded"] = False

        discovered_gemini = st.session_state.get(
            "gemini_discovered_models",
            gemini_info.get("available_models", [])
        )

        if discovered_gemini:
            curr_def_gemini = gemini_info.get("default_model") or discovered_gemini[0]
            g_def_idx = discovered_gemini.index(curr_def_gemini) if curr_def_gemini in discovered_gemini else 0

            selected_gemini_model = st.selectbox(
                "Step 3: Select Default Model",
                options=discovered_gemini,
                index=g_def_idx,
                key="select_gemini_model"
            )
        else:
            selected_gemini_model = None
            st.info("ℹ️ Models have not been loaded yet. Please complete Step 1 & Step 2 above.")

        key_ref_gemini = st.text_input(
            "Key Reference Identifier (Secure reference code)",
            value=gemini_info.get("key_reference_code", "GEMINI_API_KEY"),
            key="gemini_key_ref"
        )

        can_save_gemini = test_passed_gemini and bool(discovered_gemini) and bool(selected_gemini_model)

        btn_save_gemini = st.button(
            "💾 Step 4: Save Configuration",
            key="btn_save_gemini",
            disabled=not can_save_gemini
        )

        if btn_save_gemini:
            with st.spinner("Encrypting credential server-side and persisting configuration..."):
                c, save_res = call_api("POST", "/control-center/save-configuration", {
                    "provider": "gemini",
                    "default_model": selected_gemini_model,
                    "key_reference_code": key_ref_gemini,
                    "api_key": gemini_key_input if gemini_key_input else None,
                    "discovered_models": discovered_gemini
                })
                if c == 200:
                    csharp_persisted = save_res.get("csharp_persisted", False)
                    if csharp_persisted:
                        st.success(f"✅ {save_res.get('message')}")
                        st.info("🔒 Credential encrypted server-side and committed centrally to AIDatabase.")
                    else:
                        st.warning(f"⚠️ {save_res.get('message')}")
                        if save_res.get("csharp_error"):
                            st.caption(f"Central Database Notice: `{save_res.get('csharp_error')}`")
                    st.session_state["input_gemini_key"] = ""
                else:
                    st.error(f"Failed to save: {save_res}")


# =========================================================
# SECTION C: DIAGNOSTICS
# =========================================================
elif section == "🔍 Diagnostics":
    st.header("Operational Diagnostics & Telemetry")
    st.markdown("Detailed diagnostic status, authentication logs, and latency telemetry.")

    code, diagnostics = call_api("GET", "/control-center/diagnostics")

    if code == 200:
        for prov_name, diag in diagnostics.items():
            with st.expander(f"📌 Diagnostics: {prov_name.upper()}", expanded=True):
                c1, c2, c3, c4 = st.columns(4)
                c1.write(f"**Configured:** `{'Yes' if diag.get('configured') else 'No'}`")
                c2.write(f"**Tested:** `{'Yes' if diag.get('tested') else 'No'}`")
                c3.write(f"**Auth Status:** `{diag.get('auth_status')}`")
                c4.write(f"**Discovery:** `{diag.get('discovery_status')}`")

                st.write(f"**Inference Status:** `{diag.get('inference_status')}`")
                if diag.get("duration_ms"):
                    st.write(f"**Last Test Duration:** `{diag.get('duration_ms')} ms`")
                if diag.get("correlation_id"):
                    st.write(f"**Correlation ID:** `{diag.get('correlation_id')}`")
                if diag.get("error_category"):
                    st.warning(f"**Error Category:** `{diag.get('error_category')}`")
                st.caption(f"Last Action: {diag.get('last_action_timestamp')}")
    else:
        st.error("Failed to load diagnostic telemetry from backend.")


# =========================================================
# SECTION D: SAVED CONFIGURATION
# =========================================================
elif section == "💾 Saved Configuration":
    st.header("Saved Configuration Metadata")
    st.markdown("Inspect active providers, default model routes, and key references. Raw keys are never shown.")

    code, providers = call_api("GET", "/control-center/status")

    if code == 200:
        for prov_name, prov in providers.items():
            st.subheader(f"Provider: {prov.get('display_name', prov_name)}")
            col_s1, col_s2, col_s3 = st.columns(3)
            col_s1.write(f"**Active Status:** `{'Configured (Encrypted Credential Saved)' if prov.get('has_encrypted_credential') else ('Configured' if prov.get('is_configured') else 'Pending')}`")
            col_s2.write(f"**Default Model:** `{prov.get('default_model') or 'Not selected'}`")
            col_s3.write(f"**Key Reference Code:** `{prov.get('key_reference_code') or 'None'}`")

            st.write(f"**Available Model Catalog ({len(prov.get('available_models', []))})**:")
            models = prov.get("available_models", [])
            if models:
                st.code(", ".join(models), language="text")
            else:
                st.caption("No models discovered yet. Complete Test Connection & Load Models in Provider Configuration.")
            st.divider()
    else:
        st.error("Failed to load saved configurations from backend.")
