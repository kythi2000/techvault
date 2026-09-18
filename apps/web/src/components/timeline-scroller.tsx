"use client";

import { useEffect, useId, useRef, useState, type ReactNode } from "react";

export function TimelineScroller({ children }: { children: ReactNode }) {
  const viewport = useRef<HTMLDivElement>(null);
  const id = useId();
  const [edges, setEdges] = useState({ previous: false, next: false });

  useEffect(() => {
    const element = viewport.current;
    if (!element) return;
    const update = () => setEdges({
      previous: element.scrollLeft > 2,
      next: element.scrollLeft + element.clientWidth < element.scrollWidth - 2,
    });
    const observer = new ResizeObserver(update);
    observer.observe(element);
    element.addEventListener("scroll", update, { passive: true });
    update();
    return () => {
      observer.disconnect();
      element.removeEventListener("scroll", update);
    };
  }, [children]);

  function scroll(direction: number) {
    const element = viewport.current;
    if (!element) return;
    element.scrollBy({
      left: direction * element.clientWidth * 0.8,
      behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth",
    });
  }

  return (
    <div className="timeline-scroller">
      <div className="timeline-controls">
        <p id={`${id}-help`}>Scroll across the years, or focus the timeline and use the arrow keys.</p>
        <div>
          <button type="button" className="button button-light" aria-controls={id} disabled={!edges.previous} onClick={() => scroll(-1)}>← Earlier</button>
          <button type="button" className="button button-light" aria-controls={id} disabled={!edges.next} onClick={() => scroll(1)}>Later →</button>
        </div>
      </div>
      <div ref={viewport} id={id} className="timeline-viewport" role="region" aria-label="Devices in chronological order" tabIndex={0}>
        {children}
      </div>
    </div>
  );
}
