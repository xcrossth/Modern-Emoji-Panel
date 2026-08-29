import { IncrementalRenderer } from "../core/incremental-renderer";
import { RENDERER_ATTRIBUTE } from "../core/dom-renderer";
import { ensureRendererStyles } from "../core/renderer-styles";

interface DomFixtureReport {
  readonly fontLoaded: boolean;
  readonly initialTextPreserved: boolean;
  readonly selectionPreserved: boolean;
  readonly browserFindSucceeded: boolean;
  readonly editablePreserved: boolean;
  readonly codePreserved: boolean;
  readonly dynamicTextPreserved: boolean;
  readonly wrapperCount: number;
  readonly metrics: Readonly<IncrementalRenderer["metrics"]>;
}

const initial = document.querySelector("#initial")!;
const initialText = initial.textContent!;
const dynamic = document.querySelector("#dynamic")!;
const transcript = document.querySelector("#transcript")!;
const fontUrl = new URL("../assets/fonts/Noto-COLRv1.ttf", location.href).href;
ensureRendererStyles(document, fontUrl);
const renderer = new IncrementalRenderer(document, { maxNodesPerBatch: 50 });

const settle = () => new Promise<void>(resolve => setTimeout(resolve, 0));

async function runFixture(): Promise<DomFixtureReport> {
  renderer.start(document.body);
  renderer.flushSynchronously();

  const received = document.createElement("p");
  received.textContent = "รับข้อความใหม่ 👩🏽‍💻";
  dynamic.append(received);
  const edited = document.createElement("p");
  edited.textContent = "ข้อความเดิม";
  dynamic.append(edited);
  await settle();
  renderer.flushSynchronously();
  edited.firstChild!.textContent = "ข้อความที่แก้แล้ว 🫯";

  history.pushState({}, "", "#conversation-2");
  const room = document.createElement("p");
  room.textContent = "เปลี่ยนห้อง 🇹🇭";
  dynamic.append(room);
  const historyFragment = document.createDocumentFragment();
  for (let index = 0; index < 600; index += 1) {
    const message = document.createElement("p");
    message.textContent = `ประวัติ ${index} 🫩`;
    historyFragment.append(message);
  }
  transcript.prepend(historyFragment);
  await settle();
  renderer.flushSynchronously();
  renderer.start(document.body);
  renderer.flushSynchronously();

  const range = document.createRange();
  range.selectNodeContents(initial);
  const selection = window.getSelection()!;
  selection.removeAllRanges();
  selection.addRange(range);
  const selected = selection.toString();
  const browserWindow = window as typeof window & { find?: (text: string) => boolean };
  const find = typeof browserWindow.find === "function" && browserWindow.find("🫯");
  await document.fonts.ready;

  return {
    fontLoaded: document.fonts.check('24px "ModernEmojiNoto"'),
    initialTextPreserved: initial.textContent === initialText,
    selectionPreserved: selected === initialText,
    browserFindSucceeded: find,
    editablePreserved: document.querySelector("#composer")?.childNodes[0]?.nodeType === Node.TEXT_NODE,
    codePreserved: document.querySelector("#code")?.childNodes[0]?.nodeType === Node.TEXT_NODE,
    dynamicTextPreserved: dynamic.textContent === "รับข้อความใหม่ 👩🏽‍💻ข้อความที่แก้แล้ว 🫯เปลี่ยนห้อง 🇹🇭",
    wrapperCount: document.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`).length,
    metrics: { ...renderer.metrics },
  };
}

(window as typeof window & { collectDomFixtureReport?: () => Promise<DomFixtureReport> })
  .collectDomFixtureReport = runFixture;
