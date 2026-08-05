# Code Citations

## License: unknown
https://github.com/AnaSuarez180/Proyecto_U5_Segunda_Entrega/blob/9ad9a770745d7a205f04cdc785bbf26f1800ec54/static/static/js/index.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method:
```


## License: unknown
https://github.com/talibffdc/Project-Reactjs/blob/b33ee8037fabebb58c21cdae5936d392da977ceb/src/script.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method:
```


## License: unknown
https://github.com/AnaSuarez180/Proyecto_U5_Segunda_Entrega/blob/9ad9a770745d7a205f04cdc785bbf26f1800ec54/static/static/js/index.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/
```


## License: unknown
https://github.com/talibffdc/Project-Reactjs/blob/b33ee8037fabebb58c21cdae5936d392da977ceb/src/script.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/
```


## License: unknown
https://github.com/AnaSuarez180/Proyecto_U5_Segunda_Entrega/blob/9ad9a770745d7a205f04cdc785bbf26f1800ec54/static/static/js/index.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username:
```


## License: unknown
https://github.com/talibffdc/Project-Reactjs/blob/b33ee8037fabebb58c21cdae5936d392da977ceb/src/script.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username:
```


## License: unknown
https://github.com/AnaSuarez180/Proyecto_U5_Segunda_Entrega/blob/9ad9a770745d7a205f04cdc785bbf26f1800ec54/static/static/js/index.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .
```


## License: unknown
https://github.com/talibffdc/Project-Reactjs/blob/b33ee8037fabebb58c21cdae5936d392da977ceb/src/script.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .
```


## License: unknown
https://github.com/Master-Max/linearEditFrontend/blob/07179bb36cdd50abea83307af0a883c20296bcb2/src/apis/UserAdapter.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .then(function(data) {
        if (data.success) {
            // Logged in — redirect or update UI
            window.location.href = "/dashboard";
        }
    });
}
```

---

## Complete minimal example (copy-paste ready)

Put this script in your web app's login page (or load it on every page):

```javascript
(function() {
    // Only runs inside the kiosk app — harmless in a normal browser
    if (!window.chrome || !window.chrome.webview) return;

    // Step 1: Listen for credentials coming from the native app
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;
        if (msg && msg.type === "credentials") {
            loginWithCredentials(msg.username, msg.password);
        }
    });

    // Step 2: Ask for credentials when logged out
    function tryAutoLogin() {
        if (!isLoggedIn()) {
            window.chrome.webview.postMessage({ type: "request-credentials" });
        }
    }

    // Step 3: Your login logic
    function loginWithCredentials(username, password) {
        fetch("/api/auth/login", {
            method: "POST",
```


## License: unknown
https://github.com/kirstybrews/filmboard-frontend/blob/577a93f6fa3c72c1f2faf29656994e210af8dd33/src/components/LoginForm.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .then(function(data) {
        if (data.success) {
            // Logged in — redirect or update UI
            window.location.href = "/dashboard";
        }
    });
}
```

---

## Complete minimal example (copy-paste ready)

Put this script in your web app's login page (or load it on every page):

```javascript
(function() {
    // Only runs inside the kiosk app — harmless in a normal browser
    if (!window.chrome || !window.chrome.webview) return;

    // Step 1: Listen for credentials coming from the native app
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;
        if (msg && msg.type === "credentials") {
            loginWithCredentials(msg.username, msg.password);
        }
    });

    // Step 2: Ask for credentials when logged out
    function tryAutoLogin() {
        if (!isLoggedIn()) {
            window.chrome.webview.postMessage({ type: "request-credentials" });
        }
    }

    // Step 3: Your login logic
    function loginWithCredentials(username, password) {
        fetch("/api/auth/login", {
            method: "POST",
```


## License: unknown
https://github.com/Master-Max/linearEditFrontend/blob/07179bb36cdd50abea83307af0a883c20296bcb2/src/apis/UserAdapter.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .then(function(data) {
        if (data.success) {
            // Logged in — redirect or update UI
            window.location.href = "/dashboard";
        }
    });
}
```

---

## Complete minimal example (copy-paste ready)

Put this script in your web app's login page (or load it on every page):

```javascript
(function() {
    // Only runs inside the kiosk app — harmless in a normal browser
    if (!window.chrome || !window.chrome.webview) return;

    // Step 1: Listen for credentials coming from the native app
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;
        if (msg && msg.type === "credentials") {
            loginWithCredentials(msg.username, msg.password);
        }
    });

    // Step 2: Ask for credentials when logged out
    function tryAutoLogin() {
        if (!isLoggedIn()) {
            window.chrome.webview.postMessage({ type: "request-credentials" });
        }
    }

    // Step 3: Your login logic
    function loginWithCredentials(username, password) {
        fetch("/api/auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
```


## License: unknown
https://github.com/kirstybrews/filmboard-frontend/blob/577a93f6fa3c72c1f2faf29656994e210af8dd33/src/components/LoginForm.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .then(function(data) {
        if (data.success) {
            // Logged in — redirect or update UI
            window.location.href = "/dashboard";
        }
    });
}
```

---

## Complete minimal example (copy-paste ready)

Put this script in your web app's login page (or load it on every page):

```javascript
(function() {
    // Only runs inside the kiosk app — harmless in a normal browser
    if (!window.chrome || !window.chrome.webview) return;

    // Step 1: Listen for credentials coming from the native app
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;
        if (msg && msg.type === "credentials") {
            loginWithCredentials(msg.username, msg.password);
        }
    });

    // Step 2: Ask for credentials when logged out
    function tryAutoLogin() {
        if (!isLoggedIn()) {
            window.chrome.webview.postMessage({ type: "request-credentials" });
        }
    }

    // Step 3: Your login logic
    function loginWithCredentials(username, password) {
        fetch("/api/auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
```


## License: unknown
https://github.com/Master-Max/linearEditFrontend/blob/07179bb36cdd50abea83307af0a883c20296bcb2/src/apis/UserAdapter.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .then(function(data) {
        if (data.success) {
            // Logged in — redirect or update UI
            window.location.href = "/dashboard";
        }
    });
}
```

---

## Complete minimal example (copy-paste ready)

Put this script in your web app's login page (or load it on every page):

```javascript
(function() {
    // Only runs inside the kiosk app — harmless in a normal browser
    if (!window.chrome || !window.chrome.webview) return;

    // Step 1: Listen for credentials coming from the native app
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;
        if (msg && msg.type === "credentials") {
            loginWithCredentials(msg.username, msg.password);
        }
    });

    // Step 2: Ask for credentials when logged out
    function tryAutoLogin() {
        if (!isLoggedIn()) {
            window.chrome.webview.postMessage({ type: "request-credentials" });
        }
    }

    // Step 3: Your login logic
    function loginWithCredentials(username, password) {
        fetch("/api/auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username: username, password: password })
        })
        .then(r
```


## License: unknown
https://github.com/kirstybrews/filmboard-frontend/blob/577a93f6fa3c72c1f2faf29656994e210af8dd33/src/components/LoginForm.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .then(function(data) {
        if (data.success) {
            // Logged in — redirect or update UI
            window.location.href = "/dashboard";
        }
    });
}
```

---

## Complete minimal example (copy-paste ready)

Put this script in your web app's login page (or load it on every page):

```javascript
(function() {
    // Only runs inside the kiosk app — harmless in a normal browser
    if (!window.chrome || !window.chrome.webview) return;

    // Step 1: Listen for credentials coming from the native app
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;
        if (msg && msg.type === "credentials") {
            loginWithCredentials(msg.username, msg.password);
        }
    });

    // Step 2: Ask for credentials when logged out
    function tryAutoLogin() {
        if (!isLoggedIn()) {
            window.chrome.webview.postMessage({ type: "request-credentials" });
        }
    }

    // Step 3: Your login logic
    function loginWithCredentials(username, password) {
        fetch("/api/auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username: username, password: password })
        })
        .then(r
```


## License: unknown
https://github.com/Master-Max/linearEditFrontend/blob/07179bb36cdd50abea83307af0a883c20296bcb2/src/apis/UserAdapter.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .then(function(data) {
        if (data.success) {
            // Logged in — redirect or update UI
            window.location.href = "/dashboard";
        }
    });
}
```

---

## Complete minimal example (copy-paste ready)

Put this script in your web app's login page (or load it on every page):

```javascript
(function() {
    // Only runs inside the kiosk app — harmless in a normal browser
    if (!window.chrome || !window.chrome.webview) return;

    // Step 1: Listen for credentials coming from the native app
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;
        if (msg && msg.type === "credentials") {
            loginWithCredentials(msg.username, msg.password);
        }
    });

    // Step 2: Ask for credentials when logged out
    function tryAutoLogin() {
        if (!isLoggedIn()) {
            window.chrome.webview.postMessage({ type: "request-credentials" });
        }
    }

    // Step 3: Your login logic
    function loginWithCredentials(username, password) {
        fetch("/api/auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username: username, password: password })
        })
        .then(r => r.json())
        .
```


## License: unknown
https://github.com/kirstybrews/filmboard-frontend/blob/577a93f6fa3c72c1f2faf29656994e210af8dd33/src/components/LoginForm.js

```
## How the web app receives login credentials

The native app (Screen Manager) and the web page running inside it communicate through a **built-in message channel** that Microsoft Edge WebView2 provides. Think of it like a private telephone line between the Windows app and the web page — no internet needed, no API calls, completely internal.

---

## The two sides explained simply

```
┌─────────────────────────────┐          ┌──────────────────────────────┐
│   Windows Screen Manager    │          │      Your Web App (page)     │
│   (C# / native app)         │◄────────►│   (JavaScript in browser)    │
│                             │  message │                              │
│  Holds the encrypted        │  channel │  Sends a request when it     │
│  username + password.       │          │  detects it's logged out.    │
│  Decrypts & sends on        │          │  Receives credentials and    │
│  valid request.             │          │  logs in automatically.      │
└─────────────────────────────┘          └──────────────────────────────┘
```

---

## Step-by-step flow

### 1. Web app detects it's logged out

When the page loads and sees the user isn't logged in, it sends a message to the native app:

```javascript
// In your web app JavaScript
// Call this when you detect the user is not logged in
// (e.g. on page load, check if your auth token/session exists)

function requestAutoLogin() {
    // window.chrome.webview is only available inside WebView2 (the kiosk app)
    // Always check it exists first so the same code works in a normal browser too
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: "request-credentials" });
    }
}

// Example: call it when the login page loads
document.addEventListener("DOMContentLoaded", function() {
    if (!isUserLoggedIn()) {   // your own check
        requestAutoLogin();
    }
});
```

---

### 2. Native app verifies and responds

The Screen Manager receives that message, checks:
- Is auto-login enabled for this screen? ✓
- Does the page URL match the configured kiosk URL? ✓ (security check — stops rogue pages from stealing credentials)
- Are credentials stored? ✓

Then it sends back:

```json
{
  "type": "credentials",
  "username": "kiosk_user",
  "password": "the_actual_password"
}
```

---

### 3. Web app receives credentials and logs in

```javascript
// In your web app JavaScript — set this up once when the page loads

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;

        if (msg.type === "credentials") {
            // You have the username and password — now log in
            performLogin(msg.username, msg.password);
        }
    });
}

function performLogin(username, password) {
    // Option A: Fill and submit your existing login form
    document.getElementById("username-input").value = username;
    document.getElementById("password-input").value = password;
    document.getElementById("login-form").submit();

    // Option B: Call your login API directly (cleaner)
    fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password })
    })
    .then(function(response) { return response.json(); })
    .then(function(data) {
        if (data.success) {
            // Logged in — redirect or update UI
            window.location.href = "/dashboard";
        }
    });
}
```

---

## Complete minimal example (copy-paste ready)

Put this script in your web app's login page (or load it on every page):

```javascript
(function() {
    // Only runs inside the kiosk app — harmless in a normal browser
    if (!window.chrome || !window.chrome.webview) return;

    // Step 1: Listen for credentials coming from the native app
    window.chrome.webview.addEventListener("message", function(event) {
        var msg = event.data;
        if (msg && msg.type === "credentials") {
            loginWithCredentials(msg.username, msg.password);
        }
    });

    // Step 2: Ask for credentials when logged out
    function tryAutoLogin() {
        if (!isLoggedIn()) {
            window.chrome.webview.postMessage({ type: "request-credentials" });
        }
    }

    // Step 3: Your login logic
    function loginWithCredentials(username, password) {
        fetch("/api/auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username: username, password: password })
        })
        .then(r => r.json())
        .
```

