import { useLayoutEffect, useRef } from "react";
import gsap from "gsap";
import { ScrollTrigger } from "gsap/ScrollTrigger";

gsap.registerPlugin(ScrollTrigger);

interface ScrollRevealOptions {
  /** Distance (px) each element rises from as it reveals. */
  y?: number;
  /** Reveal tween duration in seconds. */
  duration?: number;
  /** Delay between items that enter the viewport together. */
  stagger?: number;
  /** ScrollTrigger start position (element vs. viewport). */
  start?: string;
}

/**
 * Reveal-on-scroll for a section. Returns a scope ref; mark any descendant with
 * `data-reveal` and it fades + rises into place the first time it scrolls into
 * view. Items that enter together are batched so they stagger as a group rather
 * than each firing its own trigger — a sleeker, more intentional cascade.
 *
 * Honours `prefers-reduced-motion` (content shows instantly, no transform) and
 * self-cleans via `gsap.context`, so it's safe under React 19 StrictMode.
 *
 * Usage: `const scope = useScrollReveal(); <section ref={scope}>…</section>`
 */
export function useScrollReveal<T extends HTMLElement = HTMLElement>(
  options: ScrollRevealOptions = {},
) {
  const scope = useRef<T>(null);
  const { y = 28, duration = 0.9, stagger = 0.1, start = "top 85%" } = options;

  useLayoutEffect(() => {
    if (!scope.current) return;

    const prefersReduced = window.matchMedia(
      "(prefers-reduced-motion: reduce)",
    ).matches;

    const ctx = gsap.context(() => {
      const targets = gsap.utils.toArray<HTMLElement>("[data-reveal]");
      if (!targets.length) return;

      if (prefersReduced) {
        gsap.set(targets, { opacity: 1, y: 0 });
        return;
      }

      // Hide before first paint so nothing flashes ahead of its reveal.
      gsap.set(targets, { opacity: 0, y, willChange: "transform, opacity" });

      ScrollTrigger.batch(targets, {
        start,
        once: true,
        onEnter: (batch) =>
          gsap.to(batch, {
            opacity: 1,
            y: 0,
            duration,
            stagger,
            ease: "power3.out",
            overwrite: true,
            clearProps: "willChange",
          }),
      });
    }, scope);

    return () => ctx.revert();
  }, [y, duration, stagger, start]);

  return scope;
}
