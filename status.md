# Simian Bookings — Status

## 2026-06-04 — Local dev environment fully running

### Current state
Everything is built, committed, and running locally. The API is confirmed working with all three endpoints live.

### What's built
- C# Azure Functions project (`api/`) — .NET 8 isolated worker
- `GetAvailableSlots` — checks Outlook calendar, returns free slots as UTC ISO strings
- `GetSessionTypes` — returns session list from sessions.json
- `CreateBooking` — double-checks availability, creates Outlook event with Teams meeting link
- `GraphService` — Microsoft Graph SDK, client credentials (app-only)
- `SlotCalculator` — slot generation from availability windows, UK timezone aware
- `SessionsService` — loads sessions.json at startup
- `sessions.json` — two session types: coaching-45 and coaching-60, Mon–Thu 18:00–21:00
- Static booking page (`web/index.html`) — Simian Coaching branded, 4-step flow
- `.gitignore` — excludes bin/obj and local.settings.json

### M365 App Registration
- App name: Simian Bookings
- Application (client) ID: 87768e16-e13c-4bf2-8caf-9120926111fa
- Directory (tenant) ID: 8912aa16-e5f7-40c5-b017-b26b4f252276
- Permissions required: `Calendars.ReadWrite` (Application) — admin consent granted ✅
- Client secret: stored in `api/local.settings.json` locally (NOT committed)

### How to run locally
```
cd api
func start
```
API runs on http://localhost:7071. The storage health warning is harmless — functions are HTTP-only.
Then open `web/index.html` directly in browser.

### Next session: end-to-end test
1. Confirm `Calendars.ReadWrite` admin consent is granted in Azure portal
2. Open `web/index.html` in browser with `func start` running
3. Try booking a session — verify slot availability pulls from Outlook and event + Teams link is created
4. Fix any issues found during testing
5. Then: add Google Calendar conflict checking (`GoogleCalendarService` alongside `GraphService`)
6. Then: deploy to Azure (Static Web Apps + Function App consumption plan)
