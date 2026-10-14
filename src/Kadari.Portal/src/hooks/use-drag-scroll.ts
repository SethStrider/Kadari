import { useCallback, useEffect, useRef } from "react";

/**
 * Enables mouse click-and-drag horizontal scrolling on an overflow container.
 *
 * Native overflow scrolling already handles touch drag (mobile) and the
 * wheel/trackpad, but a mouse has no drag-to-scroll — so this only kicks in for
 * `pointerType === "mouse"` and leaves touch scrolling untouched.
 *
 * Usage: `const ref = useDragScroll(); <div ref={ref} className="overflow-x-auto">`
 */
export function useDragScroll<T extends HTMLElement = HTMLDivElement>() {
  const ref = useRef<T | null>(null);
  const state = useRef({ down: false, startX: 0, startScrollLeft: 0 });

  const onPointerDown = useCallback((e: PointerEvent) => {
    const el = ref.current;
    if (!el || e.pointerType !== "mouse" || e.button !== 0) return;
    state.current = {
      down: true,
      startX: e.clientX,
      startScrollLeft: el.scrollLeft,
    };
    el.setPointerCapture(e.pointerId);
    el.style.cursor = "grabbing";
    el.style.userSelect = "none";
  }, []);

  const onPointerMove = useCallback((e: PointerEvent) => {
    const el = ref.current;
    if (!el || !state.current.down) return;
    e.preventDefault();
    el.scrollLeft =
      state.current.startScrollLeft - (e.clientX - state.current.startX);
  }, []);

  const endDrag = useCallback((e: PointerEvent) => {
    const el = ref.current;
    if (!el || !state.current.down) return;
    state.current.down = false;
    if (el.hasPointerCapture(e.pointerId))
      el.releasePointerCapture(e.pointerId);
    el.style.cursor = "";
    el.style.userSelect = "";
  }, []);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    el.addEventListener("pointerdown", onPointerDown);
    el.addEventListener("pointermove", onPointerMove);
    el.addEventListener("pointerup", endDrag);
    el.addEventListener("pointercancel", endDrag);
    return () => {
      el.removeEventListener("pointerdown", onPointerDown);
      el.removeEventListener("pointermove", onPointerMove);
      el.removeEventListener("pointerup", endDrag);
      el.removeEventListener("pointercancel", endDrag);
    };
  }, [onPointerDown, onPointerMove, endDrag]);

  return ref;
}
