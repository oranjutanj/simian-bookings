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

What does not exist yet:

- Google Calendar conflict checking
- Production Azure deployment
- CI workflow

## How It Works

1. The browser loads session types from `GET /api/session-types`.
2. The user selects a session type and requests available slots.
3. The backend reads configured availability windows from `sessions.json`.
4. The backend queries Microsoft Graph for busy Outlook calendar periods.
5. The backend calculates free slots in UK time and returns them as UTC ISO timestamps.
6. When a booking is confirmed, the backend re-checks availability and creates an Outlook event with a Teams meeting link.

## Architecture

### Frontend

- Single static page: `web/index.html`
- No framework
- Reads API from:

  - `http://localhost:7071/api` when opened locally via `file://` or localhost
  - `/api` in deployed environments

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

- `sessions.json` controls bookable session types, durations, buffers, and weekly windows
- `api/local.settings.json` holds local Azure Functions configuration and secrets

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

If you want to watch the browser:

```powershell
cd smoke-tests
npm run test:headed
```

## Sessions Configuration

Session types are defined in `sessions.json`.

Each session includes:

- `id`
- `name`
- `description`
- `durationMinutes`
- `bufferMinutes`
- `availabilityWindows`

Availability windows are defined in UK local time using:

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

## Recommended Next Steps

1. Add Google Calendar conflict checking alongside Outlook.
2. Add CI to run unit tests automatically and smoke tests on demand or before release.
3. Add deployment documentation for Azure Static Web Apps or equivalent static hosting plus Function App deployment.
4. Add more smoke coverage around the details form and non-destructive happy-path checks.

## Notes For Future Agents Or Forks

- Read `status.md` first for the latest session-by-session progress.
- Treat `sessions.json` as the main business configuration surface.
- Do not commit `api/local.settings.json`, build output, or Playwright artifacts.
- Keep unit tests and smoke tests separate: unit tests are fast and isolated, smoke tests exercise the live local app.
- Do not assume Google integration exists yet.

