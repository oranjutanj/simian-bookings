# Smoke Tests

This is a separate Playwright smoke-test suite for manual pre-build checks.

## What it covers

- Booking page loads and renders session cards from the API
- Session selection can reach the availability step without the frontend error state
- Client-side email validation works before any booking submission

## How to run

From the repo root:

```powershell
cd smoke-tests
npm install
npm run install:browsers
npm test
```

To watch the browser while running:

```powershell
cd smoke-tests
npm run test:headed
```

## Notes

- The suite starts the local Azure Functions backend automatically using `func start`.
- It opens `web/index.html` directly from disk using `file://` for a lightweight local smoke check.
- These are smoke tests, not unit tests. They exercise the live page and local backend together.
- The tests intentionally avoid creating a real booking event.
