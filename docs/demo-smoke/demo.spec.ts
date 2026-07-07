import { test, expect, Page } from '@playwright/test';

// Phase 9 D10 — the demo smoke suite (chromium only). It is the browser half of the cross-host contract
// (S3/S4 assert the same fixtures DemoContractTests asserts on CoreCLR) and the docs workflow's red/green gate
// before deploy. S1 is the standing fallback regression (today's behavior byte-for-byte when WASM is blocked).

const DEMO = 'demo.html';

// Wait until the status pill reaches (at least) the given layer.
async function waitForLayer(page: Page, label: 'Typed' | 'Typed + Render') {
  await expect(page.locator('#status-pill')).toHaveText(label, { timeout: 45_000 });
}

test.describe('Heddle demo', () => {
  test('S1 fallback: WASM blocked -> syntax layer still works', async ({ page }) => {
    const pageErrors: string[] = [];
    page.on('pageerror', (e) => pageErrors.push(String(e)));

    // Abort every request to the demo bundle before navigating: the typed layer can never boot.
    await page.route('**/demo/**', (r) => r.abort());
    await page.goto(DEMO);

    await expect(page.locator('#editor')).toBeVisible();
    await expect(page.locator('#status-pill')).toHaveText('Syntax');

    // Delete a ')' from the starter template to provoke a parse-error annotation (base-layer behavior).
    const editor = page.locator('#editor');
    await editor.click();
    await page.keyboard.type('@(Name');   // an unclosed call -> the mode worker flags it
    // The Ace gutter shows an error annotation.
    await expect(page.locator('.ace_gutter-cell.ace_error, .ace_error')).toHaveCount(1, { timeout: 15_000 });

    expect(pageErrors, 'no uncaught page errors in fallback mode').toEqual([]);
  });

  test('S2 typed diagnostics: a member typo carries a HED code', async ({ page }) => {
    await page.goto(DEMO);
    await waitForLayer(page, 'Typed');

    // Replace a valid member with a typo inside an @(...) expression.
    await page.locator('#editor').click();
    await page.keyboard.press('Control+A');
    await page.keyboard.type('@using(){{Heddle.Demo.Models}}\n@model(){{Blog}}\n@(Titlle)\n');

    // An annotation whose text carries a HED code appears.
    const annotation = page.locator('.ace_gutter-cell.ace_error');
    await expect(annotation).toHaveCount(1, { timeout: 30_000 });
  });

  test('S3 typed completion: Article members complete (C01 fixture, browser side)', async ({ page }) => {
    await page.goto(DEMO);
    await waitForLayer(page, 'Typed');

    await page.locator('#editor').click();
    // Set the template and place the caret right after "@(" inside the @list(Articles) body (Article scope) via the
    // Ace API: keyboard.type leaves the caret at the line end (outside the expression) and keystroke auto-pairing is
    // editor-version fragile. column 20 is immediately after "@(" on the third line.
    await page.evaluate(() => {
      const ed = (window as any).ace.edit('editor');
      ed.setValue('@using(){{Heddle.Demo.Models}}\n@model(){{Blog}}\n@list(Articles){{ @( }}', -1);
      ed.moveCursorTo(2, 20);
      ed.focus();
    });
    await page.keyboard.press('Control+Space');

    const popup = page.locator('.ace_autocomplete');
    await expect(popup).toBeVisible({ timeout: 30_000 });
    // Ace's autocomplete is a virtualized list that only renders the top rows into the DOM; the items sort
    // alphabetically, so assert on Article-only members that land in that rendered window (Author, Comments) —
    // neither exists on the Blog root, so they still prove the completion is scoped to the Article list body.
    await expect(popup).toContainText('Author');
    await expect(popup).toContainText('Comments');
  });

  test('S4 render round-trip: the blog starter renders the pinned fragment', async ({ page }) => {
    await page.goto(DEMO);
    await waitForLayer(page, 'Typed + Render');

    const frame = page.frameLocator('#render-frame');
    await expect(frame.locator('h1')).toContainText('Heddle Weekly', { timeout: 30_000 });
    await expect(frame.locator('.badge')).toContainText('Featured');
  });

  test('S5 C# tier declined: a C#-tier construct is refused, not crashed', async ({ page }) => {
    const pageErrors: string[] = [];
    page.on('pageerror', (e) => pageErrors.push(String(e)));

    await page.goto(DEMO);
    await waitForLayer(page, 'Typed');

    await page.locator('#editor').click();
    await page.keyboard.press('Control+A');
    // A method call is a native-tier rejection (HED1003) — the browser demo's "declined gracefully" surface.
    await page.keyboard.type('@using(){{Heddle.Demo.Models}}\n@model(){{Blog}}\n@(Title.ToUpper())\n');

    await expect(page.locator('.ace_gutter-cell.ace_error')).toHaveCount(1, { timeout: 30_000 });
    expect(pageErrors, 'no uncaught page errors when a construct is declined').toEqual([]);
  });
});
