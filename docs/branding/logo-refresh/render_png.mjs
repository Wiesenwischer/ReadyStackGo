// Renders SVG files to PNG with Chromium (Playwright from the WebUi workspace).
// Usage, from the repository root:
//   node docs/branding/logo-refresh/render_png.mjs <input.svg> <output.png> <width> [height] [background]
// Without arguments it renders the favicon and apple touch icon of the WebUi and the website.
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "..", "..", "..");
// Playwright is installed in the WebUi workspace, not next to this script.
const require = createRequire(join(root, "src", "ReadyStackGo.WebUi", "package.json"));
const { chromium } = require("@playwright/test");

async function render(page, input, output, width, height = width, background = "transparent") {
  const svg = readFileSync(input, "utf8");
  await page.setViewportSize({ width, height });
  await page.setContent(
    `<html><body style="margin:0;background:${background}">` +
      `<img src="data:image/svg+xml;base64,${Buffer.from(svg).toString("base64")}" ` +
      `style="display:block;width:${width}px;height:${height}px"></body></html>`,
  );
  await page.waitForLoadState("load");
  await page.screenshot({ path: output, omitBackground: background === "transparent" });
  console.log("rendered", output);
}

const browser = await chromium.launch();
const page = await browser.newPage({ deviceScaleFactor: 1 });
const args = process.argv.slice(2);
if (args.length >= 3) {
  await render(page, args[0], args[1], Number(args[2]), Number(args[3] ?? args[2]), args[4] ?? "transparent");
} else {
  for (const pub of [
    join(root, "src", "ReadyStackGo.WebUi", "apps", "rsgo-generic", "public"),
    join(root, "src", "ReadyStackGo.PublicWeb", "public"),
  ]) {
    await render(page, join(pub, "favicon.svg"), join(pub, "favicon.png"), 32);
    await render(page, join(pub, "images", "logo", "readystackgo-app-icon.svg"), join(pub, "apple-touch-icon.png"), 180);
  }
}
await browser.close();
