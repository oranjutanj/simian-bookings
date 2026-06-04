# Simian Bookings — Status

## 2026-06-04 — Initial build complete (Microsoft Graph only)

### What's done
- C# Azure Functions project scaffolded (`api/`) targeting .NET 8 isolated worker
- `GetAvailableSlots` function — checks Outlook calendar, returns free slots as UTC ISO strings
- `GetSessionTypes` function — returns session list from sessions.json
- `CreateBooking` function — double-checks availability, creates Outlook event with Teams meeting link
- `GraphService` — Microsoft Graph SDK, client credentials (app-only), reads calendarView, creates events
- `SlotCalculator` — generates slots from availability windows (UK timezone aware), filters against busy periods
- `SessionsService` — loads sessions.json at startup
- `sessions.json` — two session types: coaching-45 and coaching-60, Mon–Thu 18:00–21:00
- Static booking page (`web/index.html`) — branded with Simian Coaching colours, 4-step flow (session → slot → details → confirmation)
- `.gitignore` — excludes bin/obj and local.settings.json

### M365 App Registration
- App name: Simian Bookings
- Application (client) ID: 87768e16-e13c-4bf2-8caf-9120926111fa
- Directory (tenant) ID: 8912aa16-e5f7-40c5-b017-b26b4f252276
- Permissions needed: `Calendars.ReadWrite` (Application), admin consent granted
- Client secret: stored in `api/local.settings.json` locally (NOT committed)

### Next steps
1. **Add client secret** to `api/local.settings.json` (field: `ClientSecret`)
2. **Install Azure Functions Core Tools** if not already: `npm i -g azure-functions-core-tools@4`
3. **Run locally**: `cd api && func start` — API on http://localhost:7071
4. **Open** `web/index.html` in browser to test end-to-end
5. **Google Calendar** integration — to be added later (same pattern, additional service)
6. **Deploy** to Azure (Static Web Apps for frontend, Function App for backend)
