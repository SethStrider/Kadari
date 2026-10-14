/**
 * Opens the device camera and binds it to a video element (adapted from PoziSnap). Prefers the rear camera
 * on a phone (the one pointed at the room). The returned handle stops every track — call it on unmount, even
 * on the failure path, or the camera light stays on.
 */
export interface CameraHandle {
  readonly stream: MediaStream;
  stop(): void;
}

export async function startCamera(
  video: HTMLVideoElement,
): Promise<CameraHandle> {
  if (!navigator.mediaDevices?.getUserMedia) {
    throw new Error(
      "This browser does not expose a camera (getUserMedia unavailable).",
    );
  }

  const stream = await navigator.mediaDevices.getUserMedia({
    video: {
      facingMode: { ideal: "environment" },
      width: { ideal: 1280 },
      height: { ideal: 720 },
    },
    audio: false,
  });

  video.srcObject = stream;
  await video.play();

  return {
    stream,
    stop() {
      for (const track of stream.getTracks()) {
        track.stop();
      }
      video.srcObject = null;
    },
  };
}

/** Grabs the current video frame as a JPEG blob for upload — the still that becomes the evidence image. */
export async function captureFrame(
  video: HTMLVideoElement,
  maxEdge = 1280,
  quality = 0.85,
): Promise<Blob> {
  const width = video.videoWidth;
  const height = video.videoHeight;
  if (width === 0 || height === 0) {
    throw new Error("The camera has no frame yet.");
  }

  const scale = Math.min(1, maxEdge / Math.max(width, height));
  const canvas = document.createElement("canvas");
  canvas.width = Math.round(width * scale);
  canvas.height = Math.round(height * scale);

  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error("Could not get a 2D canvas context.");
  }
  context.drawImage(video, 0, 0, canvas.width, canvas.height);

  return new Promise<Blob>((resolve, reject) => {
    canvas.toBlob(
      (blob) =>
        blob ? resolve(blob) : reject(new Error("Frame encoding failed.")),
      "image/jpeg",
      quality,
    );
  });
}
