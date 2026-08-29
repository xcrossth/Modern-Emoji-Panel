import { IncrementalRenderer } from "../core/incremental-renderer";
import { identifyPrimarySite } from "../sites/site-context";

function startRenderer(): void {
  if (!identifyPrimarySite(new URL(location.href))) return;
  const renderer = new IncrementalRenderer(document);
  renderer.start();
}

if (document.documentElement) startRenderer();
else document.addEventListener("DOMContentLoaded", startRenderer, { once: true });
