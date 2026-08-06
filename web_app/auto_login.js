/**
 * OrderBuddy Kiosk Auto-Login Bridge (WebView2)
 *
 * Flow:
 * 1) If page is logged out, request credentials from native Windows app.
 * 2) Wait for ONE "credentials" message reply.
 * 3) Login with username/password/kioskId.
 *
 * Anti-spam controls:
 * - MAX_REQUEST_ATTEMPTS limits total requests per page load.
 * - REQUEST_COOLDOWN_MS enforces delay between attempts.
 * - Per-attempt timeout stops waiting forever.
 */

const KioskAutoLoginConfig = {
    MAX_REQUEST_ATTEMPTS: 2,    // total credential requests allowed per page load
    REQUEST_COOLDOWN_MS: 15000, // wait 15s between attempts if no response
    REQUEST_TIMEOUT_MS: 8000    // each attempt waits up to 8s for native response
};

let credentialRequestAttempts = 0;
let lastCredentialRequestAt = 0;
let autoLoginCompleted = false;

/**
 * Returns true if enough time has passed since the last request.
 */
function canSendCredentialRequestNow() {
    const now = Date.now();
    return (now - lastCredentialRequestAt) >= KioskAutoLoginConfig.REQUEST_COOLDOWN_MS;
}

/**
 * Requests credentials ONE time and waits for ONE credentials message.
 * This function does NOT loop; caller controls retry policy.
 */
async function requestCredentialsOnce(timeoutMs = KioskAutoLoginConfig.REQUEST_TIMEOUT_MS) {
    const webview = window.chrome && window.chrome.webview;
    if (!webview) {
        throw new Error("Not running inside WebView2 host.");
    }

    return new Promise((resolve, reject) => {
        let done = false;

        // If native app does not reply in time, stop waiting and fail this attempt.
        const timer = setTimeout(() => {
            if (done) return;
            done = true;
            webview.removeEventListener("message", onMessage);
            reject(new Error("Timed out waiting for credentials."));
        }, timeoutMs);

        function onMessage(event) {
            const msg = event && event.data;

            // Ignore unrelated messages (heartbeat/admin/etc.).
            if (!msg || msg.type !== "credentials") return;

            done = true;
            clearTimeout(timer);
            webview.removeEventListener("message", onMessage);

            // Expected payload from Windows app:
            // { type: "credentials", username, password, kioskId }
            resolve({
                username: msg.username || "",
                password: msg.password || "",
                kioskId: msg.kioskId || ""
            });
        }

        // Start listening BEFORE sending request (avoid race condition).
        webview.addEventListener("message", onMessage);

        // Send exactly one credential request for this attempt.
        webview.postMessage({ type: "request-credentials" });
    });
}

/**
 * Replace with your real logout detection logic.
 * Must return true only when user is definitely not authenticated.
 */
function isLoggedOut() {
    return !document.cookie.includes("session=");
}

/**
 * Your login API call using credentials returned by native app.
 */
async function loginWithCredentials(creds) {
    const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({
            username: creds.username,
            password: creds.password,
            kioskId: creds.kioskId // important for multi-device locations
        })
    });

    if (!res.ok) {
        throw new Error(`Login failed: HTTP ${res.status}`);
    }
}

/**
 * Main entrypoint.
 * Sends minimum requests and stops retrying when:
 * - login succeeds
 * - max attempts reached
 * - user is no longer logged out
 */
async function autoLoginIfLoggedOut() {
    // Stop if already completed in this page lifecycle.
    if (autoLoginCompleted) return;

    // Only run for logged-out pages.
    if (!isLoggedOut()) {
        autoLoginCompleted = true;
        return;
    }

    // Respect max attempt limit.
    if (credentialRequestAttempts >= KioskAutoLoginConfig.MAX_REQUEST_ATTEMPTS) {
        console.warn("Auto-login skipped: max credential request attempts reached.");
        return;
    }

    // Respect cooldown to prevent rapid repeated requests.
    if (!canSendCredentialRequestNow()) {
        console.info("Auto-login request suppressed by cooldown.");
        return;
    }

    credentialRequestAttempts += 1;
    lastCredentialRequestAt = Date.now();

    try {
        const creds = await requestCredentialsOnce();

        // If native app replied but creds are empty, do not loop aggressively.
        if (!creds.username || !creds.password) {
            console.warn("Credentials response missing username/password.");
            return;
        }

        await loginWithCredentials(creds);

        // Mark complete so we never re-request on this page instance.
        autoLoginCompleted = true;

        // Optional: reload page after successful login.
        window.location.reload();
    } catch (err) {
        console.error("Auto-login attempt failed:", err);

        // Optional single delayed retry (only if attempts remain).
        if (credentialRequestAttempts < KioskAutoLoginConfig.MAX_REQUEST_ATTEMPTS) {
            setTimeout(() => {
                // Retry only if still logged out and not completed.
                if (!autoLoginCompleted && isLoggedOut()) {
                    autoLoginIfLoggedOut();
                }
            }, KioskAutoLoginConfig.REQUEST_COOLDOWN_MS);
        }
    }
}

// Run once when page loads.
document.addEventListener("DOMContentLoaded", autoLoginIfLoggedOut);