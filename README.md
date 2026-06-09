# Simian Bookings

A lightweight custom booking system for Simian Coaching.

The goal is to replace the unreliable Microsoft Bookings plus Google calendar sync with a simple branded booking flow that checks availability directly and creates Outlook calendar events with Teams links.

## Current Scope

What exists today:

- C# Azure Functions backend in `api/`
- Static booking page in `web/`
- Session configuration in `sessions.json`
- Unit test suite in `tests/SimianBookings.Tests`
- Browser smoke-test suite in `smoke-tests/`
- Global availability windows in `sessions.json` (shared across all session types)

What does not exist yet:

- Fully validated production booking flow after first live deployment

## How It Works

1. The browser loads session types from `GET /api/session-types`.
2. The user selects a session type and requests available slots.
3. The backend reads global availability windows from `sessions.json`.
4. The backend queries Microsoft Graph for busy Outlook calendar periods.
5. The backend calculates free slots in UK time and returns them as UTC ISO timestamps.
6. When a booking is confirmed, the backend re-checks availability and creates an Outlook event with a Teams meeting link.

## Architecture

### Frontend

- Static HTML shell: `web/index.html`
- Frontend logic: `web/booking.js`
- Runtime config: `web/runtime-config.js` (generated in deploy from GitHub variable `PROD_API_BASE_URL`)
- No framework
- Reads API from:

  - `http://localhost:7071/api` when opened locally via `file://` or localhost
  - `window.SIMIAN_CONFIG.apiBase` in deployed environments (written by workflow)

Important:

- `PROD_API_BASE_URL` must be set in GitHub Actions variables to the live Function API URL.
- If this variable is wrong/missing, the booking page will fail early with "Could not load sessions".
- The frontend keeps user-facing errors generic; detailed diagnostics are written to browser console and Function logs.

### Backend

- Azure Functions .NET 8 isolated worker
- Main endpoints:

  - `GET /api/session-types`
  - `GET /api/slots?sessionType=...&weeksAhead=...`
  - `POST /api/bookings`

### Core Services

- `GraphService`: Microsoft Graph calendar access and event creation
- `SessionsService`: loads session definitions from `sessions.json`
- `SlotCalculator`: converts availability windows plus busy periods into bookable UTC slots

### Configuration Model

- `sessions.json` controls global weekly availability windows plus session types, durations, and buffers
- `api/local.settings.json` holds local Azure Functions configuration and secrets
- Function logging is emitted via `ILogger` and configured for Application Insights in isolated worker startup

## Repository Layout

```text
api/               Azure Functions backend
web/               Static booking UI
tests/             C# unit tests
smoke-tests/       Playwright browser smoke tests
assets/            Branding assets
sessions.json      Session definitions and availability windows
status.md          Rolling project progress log
README.md          Project overview and handoff notes
```

## Local Development

### Prerequisites

- .NET 8 SDK
- Azure Functions Core Tools v4
- Node.js and npm for smoke tests
- A valid Azure app registration with Microsoft Graph application permissions

### Required Local Settings

The local Functions app expects these values in `api/local.settings.json`:

- `TenantId`
- `ClientId`
- `ClientSecret`
- `CalendarUserId`
- `CalendarTimeZone`

Important:

- `ClientSecret` must be the secret value, not the secret ID.
- `api/local.settings.json` is intentionally gitignored and must never be committed.

### Run The App Locally

Start the backend:

```powershell
cd api
func start
```

Then open:

- `web/index.html` directly in a browser

Notes:

- The page is designed to work when opened directly from disk for local development.
- `dotnet run` is not currently the preferred local startup path for this isolated Functions setup in this repo; `func start` is the working option.
- The Azure storage health warning is expected for this HTTP-only local workflow.

## Testing

### Unit Tests

These cover the core booking and availability logic without requiring live Graph credentials.

Run them with:

```powershell
dotnet test tests\SimianBookings.Tests\SimianBookings.Tests.csproj
```

Current coverage includes:

- slot calculation behavior
- session loading behavior
- booking endpoint request handling
- availability endpoint request handling

### Smoke Tests

These are separate browser-level checks intended to be run occasionally before a build or deployment.

Run them with:

```powershell
cd smoke-tests
npm install
npm run install:browsers
npm test
```

The smoke suite:

- starts the local backend automatically
- opens the real booking page
- checks session cards load
- checks the availability step can be reached without the frontend error state
- checks basic session-selection behavior
- checks timezone rendering behavior for international users (for example US timezones)
- checks cross-day timezone conversion when UTC slots cross a local date boundary

If you want to watch the browser:

```powershell
cd smoke-tests
npm run test:headed
```

## Sessions Configuration

Session types are defined in `sessions.json`.

The file has two top-level sections:

- `availabilityWindows` (global, applies to all session types)
- `sessionTypes` (individual session definitions)

Each session type includes:

- `id`
- `name`
- `description`
- `durationMinutes` — how long the session lasts
- `bufferMinutes` — gap reserved after the session ends before the next booking can start
- `slotIntervalMinutes` *(optional)* — how frequently start times are offered within an availability window

### How slot generation works

The backend walks each availability window (e.g. Monday 18:00–21:00 UK time) and generates candidate start times spaced `slotIntervalMinutes` apart. Each candidate is then checked against both Outlook and Google Calendar for conflicts.

A slot is blocked if the period from its start to `start + durationMinutes + bufferMinutes` overlaps any existing calendar event.

**Example:** a 45-minute session with a 15-minute buffer and `slotIntervalMinutes: 15` offers slots at 18:00, 18:15, 18:30 … but once one is booked, the 60-minute block it occupies (45 min session + 15 min buffer) will block any overlapping candidate slots.

If `slotIntervalMinutes` is not set, it defaults to `durationMinutes + bufferMinutes` — meaning one slot per block with no overlap (original behaviour).

Global availability windows are defined in UK local time using:

- `daysOfWeek`
- `startTime`
- `endTime`

This allows session types and availability to be changed without editing code.

## Known State And Caveats

- Outlook calendar checking is implemented.
- Google Calendar checking is not implemented yet.
- The current local booking flow depends on valid Microsoft Graph credentials.
- If availability loading fails locally, check the Functions terminal output first.
- A common local failure is an invalid Azure app client secret.
- A common GitHub deploy failure is Azure Functions publish-profile auth returning Kudu 401 when `SCM Basic Auth Publishing Credentials` is disabled or the publish profile secret is stale.

## Recommended Next Steps

1. Provision Azure resources manually using AZURE-MANUAL-SETUP.txt.
2. Add GitHub Actions secrets and push to main to enable automatic deploys.
3. Add more smoke coverage around the details form and non-destructive happy-path checks.
4. Add a booking horizon rule (for example no more than 8 weeks in advance) and user-facing message for longer-range requests.
6. Always display direct contact details (for example `mike@simiancoaching.co.uk`) in the booking UI for manual requests.
7. Expand timezone smoke scenarios further (for example EU/APAC locales and confirmation text assertions) beyond current US-focused coverage.

## Notes For Future Agents Or Forks

- Read `status.md` first for the latest session-by-session progress.
- Treat `sessions.json` as the main business configuration surface.
- Do not commit `api/local.settings.json`, build output, or Playwright artifacts.
- Keep unit tests and smoke tests separate: unit tests are fast and isolated, smoke tests exercise the live local app.
- Do not assume Google integration exists yet.

