# Simian Bookings — Status

## 2026-06-08 — Unit testing baseline added in VS Code

### Progress made today
- Refactored function dependencies to use interfaces for testability:
	- `IGraphService` implemented by `GraphService`
	- `ISessionsService` implemented by `SessionsService`
- Updated DI setup in `api/Program.cs` to register interface mappings.
- Added a new test project at `tests/SimianBookings.Tests` using xUnit + Moq.
- Added unit tests for:
	- `SlotCalculator` (busy slot handling, buffer behavior, minimum notice)
	- `SessionsService` (loading from configured script root and lookup behavior)
	- `CreateBooking` function (invalid payload, slot conflict, successful booking)
	- `GetAvailableSlots` function (validation, session lookup, session types endpoint)
- Verified test execution locally:
	- `dotnet test tests\\SimianBookings.Tests\\SimianBookings.Tests.csproj`
	- Result: 10 passed, 0 failed.

### Why this matters
- We now have fast, repeatable checks around the core booking logic and function request handling.
- The interface refactor makes it much easier to keep adding tests without requiring live Graph credentials.

### Next suggested steps
1. Add tests for edge cases in date/time handling (BST/GMT transitions and boundary times).
2. Add tests for `CreateBooking` OPTIONS preflight CORS response.
3. Add a GitHub Actions workflow to run tests on every push/PR.
4. Start Google calendar conflict-check integration and add tests for merged availability.

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
