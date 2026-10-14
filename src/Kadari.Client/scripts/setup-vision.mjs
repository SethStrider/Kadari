/**
 * Stages the MediaPipe vision assets into `public/vision/` so the browser loads them from our own origin —
 * no CDN at runtime, which keeps the camera working offline. Adapted from PoziSnap; Kadari needs only the
 * object detector (the face-liveness model is not part of this product).
 *
 * Runs from `predev` / `prebuild`, idempotent: the WASM bundle is copied from node_modules (so it cannot
 * drift from the installed package) and the model is downloaded once, then cached. `public/vision/` is
 * gitignored — it is derived, not source.
 */
import { createWriteStream } from "node:fs";
import { access, cp, mkdir, stat } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { Readable } from "node:stream";
import { pipeline } from "node:stream/promises";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(here, "..");

const WASM_SOURCE = resolve(webRoot, "node_modules/@mediapipe/tasks-vision/wasm");
const WASM_TARGET = resolve(webRoot, "public/vision/wasm");

/**
 * EfficientDet-Lite0, float32 (~13 MB). Its 320x320 input matches the resolution frames are downscaled to.
 * Do NOT swap for the int8 build (~4.4 MB): under the GPU delegate it returns zero detections, silently.
 */
const MODEL_URL =
  "https://storage.googleapis.com/mediapipe-models/object_detector/efficientdet_lite0/float32/1/efficientdet_lite0.tflite";
const MODEL_TARGET = resolve(webRoot, "public/vision/efficientdet_lite0_float32.tflite");

async function exists(path) {
  try {
    await access(path);
    return true;
  } catch {
    return false;
  }
}

async function copyWasm() {
  if (!(await exists(WASM_SOURCE))) {
    throw new Error(`MediaPipe WASM not found at ${WASM_SOURCE}. Run \`pnpm install\` first.`);
  }
  await cp(WASM_SOURCE, WASM_TARGET, { recursive: true });
  console.log(`[vision] wasm  -> ${WASM_TARGET}`);
}

async function downloadModel() {
  if (await exists(MODEL_TARGET)) {
    const { size } = await stat(MODEL_TARGET);
    console.log(`[vision] model -> ${MODEL_TARGET} (cached, ${(size / 1e6).toFixed(1)} MB)`);
    return;
  }

  await mkdir(dirname(MODEL_TARGET), { recursive: true });
  const response = await fetch(MODEL_URL);
  if (!response.ok || !response.body) {
    throw new Error(`Model download failed (${response.status}) from ${MODEL_URL}`);
  }

  await pipeline(Readable.fromWeb(response.body), createWriteStream(MODEL_TARGET));
  const { size } = await stat(MODEL_TARGET);
  console.log(`[vision] model -> ${MODEL_TARGET} (${(size / 1e6).toFixed(1)} MB)`);
}

await mkdir(dirname(WASM_TARGET), { recursive: true });
await copyWasm();
await downloadModel();
console.log("[vision] ready.");
