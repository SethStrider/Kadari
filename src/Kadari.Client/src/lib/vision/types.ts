/**
 * One object the on-device detector found in a frame. The box is normalised to 0..1 against the frame,
 * so it survives whatever resolution inference ran at and whatever size the still is uploaded at. This is
 * the exact shape the API's `detections` field expects (see Kadari `DetectedObject`).
 */
export interface DetectedObject {
  label: string;
  confidence: number;
  x: number;
  y: number;
  width: number;
  height: number;
}
