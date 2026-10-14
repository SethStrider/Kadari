import { useCallback, useEffect, useRef, useState } from "react";
import { Camera, Loader2 } from "lucide-react";
import { toast } from "sonner";

import { KadariButton } from "@/components/kadari/kadari-button";
import { analyzeSnap, type AnalyzeSnapResponse } from "@/lib/api/kadari";
import { ApiProblem } from "@/lib/api/problem";
import {
  startCamera,
  captureFrame,
  type CameraHandle,
} from "@/lib/vision/cameraStream";
import { AssetDetector } from "@/lib/vision/objectDetector";
import type { DetectedObject } from "@/lib/vision/types";

/** Detection cadence, deliberately below display refresh — boxes stay responsive without starving the UI. */
const DETECT_INTERVAL_MS = 1000 / 12;

interface CaptureCameraProps {
  customerId: string;
  onCaptured?: (result: AnalyzeSnapResponse) => void;
  /** The server refused the capture for want of consent (e.g. withdrawn in another tab) — re-ask. */
  onConsentRequired?: () => void;
}

/**
 * Live camera with on-device object detection (adapted from PoziSnap). Boxes are drawn on an overlay canvas;
 * "Capture" uploads the still + the current detections to the Kadari snap endpoint. Runtime camera
 * behaviour still needs a device to exercise, but the module typechecks and builds.
 */
export function CaptureCamera({
  customerId,
  onCaptured,
  onConsentRequired,
}: CaptureCameraProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const detectionsRef = useRef<DetectedObject[]>([]);

  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [count, setCount] = useState(0);
  const [roomLabel, setRoomLabel] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    let camera: CameraHandle | null = null;
    let detector: AssetDetector | null = null;
    let frame = 0;
    let lastDetect = 0;

    async function init() {
      const video = videoRef.current;
      if (!video) return;

      try {
        camera = await startCamera(video);
        detector = await AssetDetector.create();
        if (cancelled) return;
        setReady(true);
        loop();
      } catch (cause) {
        if (!cancelled) {
          setError(
            cause instanceof Error
              ? cause.message
              : "Could not start the camera.",
          );
        }
      }
    }

    function loop() {
      frame = requestAnimationFrame(loop);
      const video = videoRef.current;
      if (!video || !detector || video.readyState < 2) return;

      const now = performance.now();
      if (now - lastDetect < DETECT_INTERVAL_MS) return;
      lastDetect = now;

      const detections = detector.detect(video, now);
      detectionsRef.current = detections;
      setCount(detections.length);
      draw(detections);
    }

    function draw(detections: DetectedObject[]) {
      const video = videoRef.current;
      const canvas = canvasRef.current;
      if (!video || !canvas) return;

      const width = video.clientWidth;
      const height = video.clientHeight;
      if (canvas.width !== width) canvas.width = width;
      if (canvas.height !== height) canvas.height = height;

      const ctx = canvas.getContext("2d");
      if (!ctx) return;
      ctx.clearRect(0, 0, width, height);
      const mark =
        getComputedStyle(canvas).getPropertyValue("--color-accent").trim() ||
        "#3CC792";
      ctx.strokeStyle = mark;
      ctx.lineWidth = 2;
      ctx.font = "12px sans-serif";
      ctx.fillStyle = mark;

      for (const d of detections) {
        const x = d.x * width;
        const y = d.y * height;
        const w = d.width * width;
        const h = d.height * height;
        ctx.strokeRect(x, y, w, h);
        ctx.fillText(`${d.label} ${(d.confidence * 100) | 0}%`, x + 4, y + 14);
      }
    }

    void init();

    return () => {
      cancelled = true;
      cancelAnimationFrame(frame);
      detector?.close();
      camera?.stop();
    };
  }, []);

  const capture = useCallback(async () => {
    const video = videoRef.current;
    if (!video) return;

    setBusy(true);
    try {
      const image = await captureFrame(video);
      const result = await analyzeSnap(
        customerId,
        image,
        detectionsRef.current,
        roomLabel || undefined,
      );
      toast.success(
        result.enriched
          ? `Captured — ${result.assetIds.length} asset(s) identified.`
          : `Captured — ${result.assetIds.length} item(s) recorded (awaiting enrichment).`,
      );
      onCaptured?.(result);
    } catch (cause) {
      if (cause instanceof ApiProblem && cause.code === "consent-required") {
        onConsentRequired?.();
        return;
      }
      toast.error(cause instanceof Error ? cause.message : "Capture failed.");
    } finally {
      setBusy(false);
    }
  }, [customerId, roomLabel, onCaptured, onConsentRequired]);

  if (error) {
    return (
      <div className="rounded-(--radius-lg) border border-(--color-border) bg-(--color-surface) p-(--space-7) text-center text-(--color-text-muted)">
        <p className="font-semibold text-(--color-text)">Camera unavailable</p>
        <p className="mt-2 text-sm">{error}</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="relative overflow-hidden rounded-(--radius-lg) bg-black">
        <video ref={videoRef} playsInline muted className="h-auto w-full" />
        <canvas
          ref={canvasRef}
          className="pointer-events-none absolute inset-0 h-full w-full"
        />
        {!ready && (
          <div className="absolute inset-0 flex items-center justify-center text-white">
            <Loader2 className="size-6 animate-spin" />
          </div>
        )}
      </div>

      <div className="flex items-center gap-3">
        <input
          value={roomLabel}
          onChange={(event) => setRoomLabel(event.target.value)}
          placeholder="Room (e.g. Living room)"
          className="kd-focus h-(--tap-target-min) flex-1 rounded-(--radius-md) border border-(--color-border) bg-(--color-surface) px-(--space-5) text-sm text-(--color-text) placeholder:text-(--color-text-muted)"
        />
        <KadariButton type="button" onClick={capture} disabled={!ready || busy}>
          {busy ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <Camera className="size-4" />
          )}
          Capture{count > 0 ? ` (${count})` : ""}
        </KadariButton>
      </div>
    </div>
  );
}
