import {
  FilesetResolver,
  ObjectDetector,
  type ObjectDetectorResult,
} from "@mediapipe/tasks-vision";
import type { DetectedObject } from "./types";

/**
 * MediaPipe Object Detector, adapted from PoziSnap for Kadari. Assets are served from our own origin —
 * `pnpm setup:vision` stages the WASM + model into `public/vision/` — so the camera works offline and pulls
 * nothing from a CDN at runtime.
 *
 * EfficientDet-Lite0, float32: the int8 build is a third of the size but returns zero detections under the
 * GPU delegate, silently. Do not swap it (see scripts/setup-vision.mjs).
 */
const WASM_PATH = "/vision/wasm";
const MODEL_PATH =
  (import.meta.env.VITE_DETECTOR_MODEL as string | undefined) ??
  "/vision/efficientdet_lite0_float32.tflite";

/** EfficientDet-Lite0 runs 320x320 internally; feeding it larger frames costs copy time and buys nothing. */
export const PROCESSING_WIDTH = 320;
const SCORE_THRESHOLD = 0.4;
const MAX_RESULTS = 8;

export class AssetDetector {
  private constructor(private readonly detector: ObjectDetector) {}

  /** Loads the WASM runtime and the model. GPU delegate first, falling back to CPU. */
  static async create(): Promise<AssetDetector> {
    const fileset = await FilesetResolver.forVisionTasks(WASM_PATH);

    const build = (delegate: "GPU" | "CPU") =>
      ObjectDetector.createFromOptions(fileset, {
        baseOptions: { modelAssetPath: MODEL_PATH, delegate },
        runningMode: "VIDEO",
        scoreThreshold: SCORE_THRESHOLD,
        maxResults: MAX_RESULTS,
      });

    try {
      return new AssetDetector(await build("GPU"));
    } catch {
      // Some devices/browsers have no working GPU delegate; CPU is slower but correct.
      return new AssetDetector(await build("CPU"));
    }
  }

  /**
   * Detect on a single video frame, returning normalised boxes. `timestampMs` must strictly increase across
   * calls (MediaPipe VIDEO mode requirement) — pass `performance.now()`.
   */
  detect(video: HTMLVideoElement, timestampMs: number): DetectedObject[] {
    const width = video.videoWidth;
    const height = video.videoHeight;
    if (width === 0 || height === 0) {
      return [];
    }

    const result: ObjectDetectorResult = this.detector.detectForVideo(
      video,
      timestampMs,
    );

    return result.detections.flatMap((detection) => {
      const box = detection.boundingBox;
      const category = detection.categories[0];
      if (!box || !category) {
        return [];
      }

      return [
        {
          label: category.categoryName || category.displayName || "object",
          confidence: category.score,
          x: clamp01(box.originX / width),
          y: clamp01(box.originY / height),
          width: clamp01(box.width / width),
          height: clamp01(box.height / height),
        },
      ];
    });
  }

  close(): void {
    this.detector.close();
  }
}

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}
