import { IncrementalRenderer } from "../core/incremental-renderer";
import { RENDERER_ATTRIBUTE, renderSubtree, unwrapRenderedEmoji } from "../core/dom-renderer";
import { ensureRendererStyles } from "../core/renderer-styles";
import { SETTINGS_STORAGE_KEY, isSiteEnabled, migrateSettings, type RendererSettings } from "../settings/settings";
import { loadSettings } from "../settings/storage";

export interface RendererPageStatus {
  readonly available: true;
  readonly enabled: boolean;
  readonly hostname: string;
  readonly wrappers: number;
  readonly metrics: Readonly<IncrementalRenderer["metrics"]> | null;
}

export class ContentRendererController {
  private renderer: IncrementalRenderer | null = null;
  private settings: RendererSettings | null = null;
  private activeSignature: string | null = null;
  private staticMetrics: IncrementalRenderer["metrics"] | null = null;
  private readonly storageListener = (
    changes: Record<string, chrome.storage.StorageChange>,
    areaName: string,
  ) => {
    if (areaName !== "local" || !changes[SETTINGS_STORAGE_KEY]) return;
    void this.apply(migrateSettings(changes[SETTINGS_STORAGE_KEY].newValue));
  };

  constructor(private readonly document: Document, private readonly hostname: string) {}

  async start(): Promise<void> {
    chrome.storage.onChanged.addListener(this.storageListener);
    await this.apply(await loadSettings());
  }

  stop(): void {
    chrome.storage.onChanged.removeListener(this.storageListener);
    this.renderer?.stop();
    this.renderer = null;
    this.activeSignature = null;
  }

  async apply(settings: RendererSettings): Promise<void> {
    this.settings = settings;
    const enabled = isSiteEnabled(settings, this.hostname);
    if (!enabled) {
      this.renderer?.stop();
      this.renderer = null;
      this.activeSignature = null;
      this.staticMetrics = null;
      unwrapRenderedEmoji(this.document);
      return;
    }
    const signature = `${settings.debug}:${settings.processDynamicContent}`;
    if (this.activeSignature === signature) return;
    this.renderer?.stop();
    this.renderer = null;
    this.activeSignature = signature;
    ensureRendererStyles(
      this.document,
      chrome.runtime.getURL("assets/fonts/Noto-COLRv1.ttf"),
    );
    if (settings.processDynamicContent) {
      this.staticMetrics = null;
      this.renderer = new IncrementalRenderer(this.document, { debug: settings.debug });
      this.renderer.start();
    } else {
      const started = performance.now();
      const result = renderSubtree(this.document.documentElement);
      const elapsed = performance.now() - started;
      this.staticMetrics = {
        nodesVisited: 0,
        wrappersCreated: result.wrappersCreated,
        batches: 1,
        processingMilliseconds: elapsed,
        maxBatchMilliseconds: elapsed,
        skippedEditableNodes: result.skippedEditableNodes,
      };
      if (settings.debug) console.debug("[Modern Emoji Renderer]", { ...this.staticMetrics });
    }
  }

  status(): RendererPageStatus {
    return {
      available: true,
      enabled: this.settings ? isSiteEnabled(this.settings, this.hostname) : false,
      hostname: this.hostname,
      wrappers: this.document.querySelectorAll(`[${RENDERER_ATTRIBUTE}="emoji"]`).length,
      metrics: this.renderer ? { ...this.renderer.metrics } : this.staticMetrics ? { ...this.staticMetrics } : null,
    };
  }
}
