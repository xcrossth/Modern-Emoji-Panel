import budgets from "../../tests/performance-budgets.json";
import { IncrementalRenderer } from "../core/incremental-renderer";
import { RENDERER_ATTRIBUTE } from "../core/dom-renderer";

const makeMessages = (count: number, prefix: string): DocumentFragment => {
  const fragment = document.createDocumentFragment();
  for (let index = 0; index < count; index += 1) {
    const message = document.createElement("p");
    message.textContent = `${prefix} ${index} ไทย 🫩 English 👩🏽‍💻`;
    fragment.append(message);
  }
  return fragment;
};
const settle = () => new Promise<void>(resolve => setTimeout(resolve, 0));

async function runPerformanceFixture() {
  const initialRoot = document.querySelector("#initial")!;
  initialRoot.append(makeMessages(budgets.initialMessages, "initial"));
  const initialRenderer = new IncrementalRenderer(document, { maxNodesPerBatch: 250 });
  const initialStarted = performance.now();
  initialRenderer.start(initialRoot);
  initialRenderer.flushSynchronously();
  const initialMilliseconds = performance.now() - initialStarted;
  initialRenderer.stop();

  const burstRoot = document.querySelector("#burst")!;
  const burstRenderer = new IncrementalRenderer(document, { maxNodesPerBatch: 250 });
  burstRenderer.start(burstRoot);
  burstRenderer.flushSynchronously();
  const burstStarted = performance.now();
  burstRoot.append(makeMessages(budgets.burstMessages, "burst"));
  await settle();
  burstRenderer.flushSynchronously();
  const burstMilliseconds = performance.now() - burstStarted;
  burstRenderer.stop();

  const navigationRoot = document.querySelector("#navigation")!;
  const navigationRenderer = new IncrementalRenderer(document, { maxNodesPerBatch: 250 });
  navigationRenderer.start(navigationRoot);
  navigationRenderer.flushSynchronously();
  const navigationStarted = performance.now();
  for (let cycle = 0; cycle < budgets.navigationCycles; cycle += 1) {
    history.pushState({}, "", `#conversation-${cycle}`);
    navigationRoot.replaceChildren(makeMessages(budgets.messagesPerNavigation, `room-${cycle}`));
    navigationRenderer.start(navigationRoot);
    await settle();
    navigationRenderer.flushSynchronously();
  }
  const navigationMilliseconds = performance.now() - navigationStarted;
  const wrappersBeforeScroll = navigationRoot.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`).length;
  for (let offset = 0; offset < 100; offset += 1) navigationRoot.scrollTop = offset * 20;
  const wrappersAfterScroll = navigationRoot.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`).length;
  navigationRenderer.stop();

  return {
    budgets,
    initial: {
      milliseconds: initialMilliseconds,
      wrappers: initialRoot.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`).length,
      metrics: { ...initialRenderer.metrics },
    },
    burst: {
      milliseconds: burstMilliseconds,
      wrappers: burstRoot.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`).length,
      metrics: { ...burstRenderer.metrics },
    },
    navigation: {
      milliseconds: navigationMilliseconds,
      currentWrappers: wrappersAfterScroll,
      wrappersBeforeScroll,
      wrappersAfterScroll,
      metrics: { ...navigationRenderer.metrics },
    },
    editablePreserved: document.querySelector("#composer")?.childNodes[0]?.nodeType === Node.TEXT_NODE,
  };
}

(window as typeof window & { runPerformanceFixture?: typeof runPerformanceFixture }).runPerformanceFixture = runPerformanceFixture;
