import { classifyTextNode, renderImageElement, renderTextNode } from "./dom-renderer";

export interface RendererMetrics {
  nodesVisited: number;
  wrappersCreated: number;
  batches: number;
  processingMilliseconds: number;
  maxBatchMilliseconds: number;
  skippedEditableNodes: number;
}

interface IdleDeadlineLike { didTimeout: boolean; timeRemaining(): number }
type IdleCallback = (deadline: IdleDeadlineLike) => void;
type ScheduleIdle = (callback: IdleCallback) => number;
type CancelIdle = (handle: number) => void;

export interface IncrementalRendererOptions {
  readonly debug?: boolean;
  readonly maxNodesPerBatch?: number;
  readonly scheduleIdle?: ScheduleIdle;
  readonly cancelIdle?: CancelIdle;
  readonly now?: () => number;
}

interface ScanCursor {
  readonly root: Node;
  readonly stack: Node[];
}

const defaultScheduleIdle: ScheduleIdle = callback => {
  if (typeof globalThis.requestIdleCallback === "function") {
    return globalThis.requestIdleCallback(callback, { timeout: 100 });
  }
  return window.setTimeout(() => callback({ didTimeout: true, timeRemaining: () => 0 }), 16);
};
const defaultCancelIdle: CancelIdle = handle => {
  if (typeof globalThis.cancelIdleCallback === "function") globalThis.cancelIdleCallback(handle);
  else window.clearTimeout(handle);
};

export class IncrementalRenderer {
  readonly metrics: RendererMetrics = {
    nodesVisited: 0,
    wrappersCreated: 0,
    batches: 0,
    processingMilliseconds: 0,
    maxBatchMilliseconds: 0,
    skippedEditableNodes: 0,
  };
  private readonly cursors: ScanCursor[] = [];
  private readonly queuedRoots = new Set<Node>();
  private readonly maxNodesPerBatch: number;
  private readonly scheduleIdle: ScheduleIdle;
  private readonly cancelIdle: CancelIdle;
  private readonly now: () => number;
  private observer: MutationObserver | null = null;
  private observedRoot: Node | null = null;
  private idleHandle: number | null = null;

  constructor(private readonly document: Document, private readonly options: IncrementalRendererOptions = {}) {
    this.maxNodesPerBatch = options.maxNodesPerBatch ?? 250;
    this.scheduleIdle = options.scheduleIdle ?? defaultScheduleIdle;
    this.cancelIdle = options.cancelIdle ?? defaultCancelIdle;
    this.now = options.now ?? (() => performance.now());
  }

  start(root: Node = this.document.documentElement): void {
    if (this.observer) return;
    this.observer = new MutationObserver(records => this.handleMutations(records));
    this.observedRoot = root;
    this.observe();
    this.enqueue(root);
  }

  stop(): void {
    this.observer?.disconnect();
    this.observer = null;
    this.observedRoot = null;
    if (this.idleHandle !== null) this.cancelIdle(this.idleHandle);
    this.idleHandle = null;
    this.cursors.length = 0;
    this.queuedRoots.clear();
  }

  enqueue(root: Node): void {
    if (this.queuedRoots.has(root)) return;
    this.queuedRoots.add(root);
    this.cursors.push({ root, stack: [root] });
    this.schedule();
  }

  flushSynchronously(): void {
    while (this.cursors.length > 0) {
      this.processBatch({ didTimeout: true, timeRemaining: () => Number.POSITIVE_INFINITY });
    }
  }

  private handleMutations(records: readonly MutationRecord[]): void {
    for (const record of records) {
      if (record.type === "characterData") this.enqueue(record.target);
      for (const node of record.addedNodes) this.enqueue(node);
    }
  }

  private observe(): void {
    if (this.observer && this.observedRoot) {
      this.observer.observe(this.observedRoot, { childList: true, subtree: true, characterData: true });
    }
  }

  private schedule(): void {
    if (this.idleHandle !== null) return;
    this.idleHandle = this.scheduleIdle(deadline => {
      this.idleHandle = null;
      this.processBatch(deadline);
      if (this.cursors.length > 0) this.schedule();
    });
  }

  private processBatch(deadline: IdleDeadlineLike): void {
    const started = this.now();
    let processed = 0;
    this.metrics.batches += 1;
    this.observer?.disconnect();
    try {
      while (this.cursors.length > 0 && processed < this.maxNodesPerBatch) {
        if (!deadline.didTimeout && deadline.timeRemaining() <= 1) break;
        const cursor = this.cursors[0]!;
        const node = cursor.stack.pop() ?? null;
        if (!node) {
          this.cursors.shift();
          this.queuedRoots.delete(cursor.root);
          continue;
        }
        if (node.nodeType === node.ELEMENT_NODE && (node as Element).tagName === "IMG") {
          processed += 1;
          this.metrics.nodesVisited += 1;
          const result = renderImageElement(node as Element);
          this.metrics.wrappersCreated += result.wrappersCreated;
          this.metrics.skippedEditableNodes += result.skippedEditableNodes;
          continue;
        }
        if (node.nodeType !== node.TEXT_NODE) {
          const children = Array.from(node.childNodes);
          for (let index = children.length - 1; index >= 0; index -= 1) {
            const child = children[index];
            if (child) cursor.stack.push(child);
          }
          continue;
        }
        processed += 1;
        this.metrics.nodesVisited += 1;
        const textNode = node as Text;
        const classification = classifyTextNode(textNode);
        if (classification === "skip-editable") this.metrics.skippedEditableNodes += 1;
        if (classification !== "render") continue;
        const result = renderTextNode(textNode);
        this.metrics.wrappersCreated += result.wrappersCreated;
      }
    } finally {
      this.observe();
    }
    const elapsed = this.now() - started;
    this.metrics.processingMilliseconds += elapsed;
    this.metrics.maxBatchMilliseconds = Math.max(this.metrics.maxBatchMilliseconds, elapsed);
    if (this.options.debug) console.debug("[Modern Emoji Renderer]", { ...this.metrics });
  }
}
