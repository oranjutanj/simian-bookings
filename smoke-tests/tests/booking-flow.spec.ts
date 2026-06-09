import { expect, test, type Page } from '@playwright/test';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const bookingPageUrl = pathToFileURL(
  path.resolve(__dirname, '..', '..', 'web', 'index.html')
).href;

const mockSessions = [
  {
    id: 'coaching-45',
    name: 'Coaching Session (45 min)',
    description: 'A focused coaching session',
    durationMinutes: 45
  }
];

async function mockBookingApis(page: Page, slotIsoUtc: string) {
  await page.route('**/api/session-types', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockSessions)
    });
  });

  await page.route('**/api/slots?*', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        sessionTypeId: 'coaching-45',
        sessionName: 'Coaching Session (45 min)',
        durationMinutes: 45,
        availableSlots: [slotIsoUtc]
      })
    });
  });
}

test.describe('booking page smoke tests', () => {
  test('loads configured session cards', async ({ page }) => {
    await page.goto(bookingPageUrl);

    await expect(page.getByRole('heading', { name: 'Book a Session' })).toBeVisible();
    await expect(page.getByText('Coaching Session (45 min)')).toBeVisible();
    await expect(page.getByText('Coaching Session (60 min)')).toBeVisible();
  });

  test('can select a session and load availability step without a frontend error', async ({ page }) => {
    await page.goto(bookingPageUrl);

    await page.getByText('Coaching Session (45 min)').click();
    await page.getByRole('button', { name: 'Choose a time →' }).click();

    await expect(page.getByRole('heading', { name: 'Pick a date and time' })).toBeVisible();
    await expect(page.getByText('Could not load availability. Please try again.')).toHaveCount(0);

    const slots = page.locator('.slot-btn');
    const noSlots = page.getByText('No available slots this week — try the next week.');
    await expect(slots.first().or(noSlots)).toBeVisible();
  });

  test('choose time action stays disabled until a session is selected', async ({ page }) => {
    await page.goto(bookingPageUrl);

    const chooseTimeButton = page.getByRole('button', { name: 'Choose a time →' });

    await expect(chooseTimeButton).toBeDisabled();

    await page.getByText('Coaching Session (45 min)').click();

    await expect(chooseTimeButton).toBeEnabled();
  });

  test.describe('international timezone rendering', () => {
    test.use({ timezoneId: 'America/New_York' });

    test('shows local timezone note and slot time for US East Coast', async ({ page }) => {
      const now = new Date();
      const slotIsoUtc = new Date(Date.UTC(
        now.getUTCFullYear(),
        now.getUTCMonth(),
        now.getUTCDate() + 2,
        17,
        0,
        0
      )).toISOString();
      await mockBookingApis(page, slotIsoUtc);

      await page.goto(bookingPageUrl);
      await page.getByText('Coaching Session (45 min)').click();
      await page.getByRole('button', { name: 'Choose a time →' }).click();

      await expect(page.locator('#timezone-note')).toContainText('America/New_York');

      const expectedLocalTime = await page.evaluate(iso => {
        return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
      }, slotIsoUtc);

      await expect(page.locator('.slot-btn').first()).toHaveText(expectedLocalTime);
    });
  });

  test.describe('cross-day conversion in US Pacific', () => {
    test.use({ timezoneId: 'America/Los_Angeles' });

    test('renders the previous local day when UTC slot crosses date boundary', async ({ page }) => {
      const now = new Date();
      const slotIsoUtc = new Date(Date.UTC(
        now.getUTCFullYear(),
        now.getUTCMonth(),
        now.getUTCDate() + 1,
        0,
        30,
        0
      )).toISOString();
      await mockBookingApis(page, slotIsoUtc);

      await page.goto(bookingPageUrl);
      await page.getByText('Coaching Session (45 min)').click();
      await page.getByRole('button', { name: 'Choose a time →' }).click();

      const expectedDayLabel = await page.evaluate(iso => {
        return new Date(iso).toLocaleDateString(undefined, {
          weekday: 'long',
          day: 'numeric',
          month: 'long'
        });
      }, slotIsoUtc);

      await expect(page.locator('.day-label').first()).toHaveText(expectedDayLabel);
    });
  });
});
