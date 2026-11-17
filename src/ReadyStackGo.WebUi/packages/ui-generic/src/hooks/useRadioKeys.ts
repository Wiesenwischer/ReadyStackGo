import { useRef, type KeyboardEvent } from "react";

/**
 * Roving focus for a radio group: arrow keys, Home and End select and focus the next option.
 * Returns the key handler and a ref setter per option.
 */
export function useRadioKeys(count: number, select: (index: number) => void) {
  const refs = useRef<(HTMLButtonElement | null)[]>([]);
  const onKeyDown = (e: KeyboardEvent<HTMLButtonElement>, index: number) => {
    let next = -1;
    if (e.key === "ArrowRight" || e.key === "ArrowDown") next = (index + 1) % count;
    else if (e.key === "ArrowLeft" || e.key === "ArrowUp") next = (index - 1 + count) % count;
    else if (e.key === "Home") next = 0;
    else if (e.key === "End") next = count - 1;
    if (next < 0) return;
    e.preventDefault();
    select(next);
    refs.current[next]?.focus();
  };
  const setRef = (index: number) => (el: HTMLButtonElement | null) => {
    refs.current[index] = el;
  };
  return { onKeyDown, setRef };
}
