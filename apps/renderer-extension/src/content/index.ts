import { IncrementalRenderer } from "../core/incremental-renderer";

function startRenderer(): void {
  const renderer = new IncrementalRenderer(document);
  renderer.start();
}

if (document.documentElement) startRenderer();
else document.addEventListener("DOMContentLoaded", startRenderer, { once: true });
