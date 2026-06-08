import { expect, test } from '@playwright/test';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const bookingPageUrl = pathToFileURL(
  path.resolve(__dirname, '..', '..', 'web', 'index.html')
).href;

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
});
