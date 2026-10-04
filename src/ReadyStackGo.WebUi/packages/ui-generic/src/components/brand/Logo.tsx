// ReadyStackGo logo: isometric cube mark (design: docs/specs/theme-und-logo/entwurf, mark B1)
// and the wordmark. Cube colors are fixed brand colors in every theme; only "Stack" follows the
// background via the logo-* theme tokens. Geometry: docs/plans/theme-und-logo.md, step 4.

const EDGE = 10;
const WIDTH = EDGE * Math.sqrt(3);
const GAP = 0.9;

type CubeColor = "turquoise" | "white" | "orange";

const FACES: Record<CubeColor, { top: string; left: string; right: string; stroke?: string }> = {
  turquoise: { top: "#4CDCDF", left: "#00CED1", right: "#00A8AB" },
  white: { top: "#FFFFFF", left: "#E2E9EA", right: "#C4CFD1", stroke: "#A9B6B9" },
  orange: { top: "#FF9D77", left: "#FF6B35", right: "#E5521B" },
};

const point = (x: number, y: number) => `${x.toFixed(2)},${y.toFixed(2)}`;

function Cube({ x, y, color }: { x: number; y: number; color: CubeColor }) {
  const f = FACES[color];
  const T = point(x + WIDTH / 2, y);
  const R = point(x + WIDTH, y + EDGE / 2);
  const C = point(x + WIDTH / 2, y + EDGE);
  const L = point(x, y + EDGE / 2);
  const BL = point(x, y + 1.5 * EDGE);
  const B = point(x + WIDTH / 2, y + 2 * EDGE);
  const BR = point(x + WIDTH, y + 1.5 * EDGE);
  const stroke = f.stroke ? { stroke: f.stroke, strokeWidth: 0.6, strokeLinejoin: "round" as const } : {};
  return (
    <>
      <polygon points={`${T} ${R} ${C} ${L}`} fill={f.top} {...stroke} />
      <polygon points={`${L} ${C} ${B} ${BL}`} fill={f.left} {...stroke} />
      <polygon points={`${C} ${R} ${BR} ${B}`} fill={f.right} {...stroke} />
    </>
  );
}

const ROW_Y = 1.5 * EDGE + GAP;
const COL_X = WIDTH + GAP;

/** Mark B1: pyramid of six cubes, orange on top. */
const PYRAMID: { x: number; y: number; color: CubeColor }[] = [
  { x: COL_X, y: 0, color: "orange" },
  { x: COL_X / 2, y: ROW_Y, color: "white" },
  { x: 1.5 * COL_X, y: ROW_Y, color: "turquoise" },
  { x: 0, y: 2 * ROW_Y, color: "turquoise" },
  { x: COL_X, y: 2 * ROW_Y, color: "white" },
  { x: 2 * COL_X, y: 2 * ROW_Y, color: "turquoise" },
];
const PYRAMID_SIZE = { w: 3 * WIDTH + 2 * GAP, h: 2 * ROW_Y + 2 * EDGE };

/** Small mark for 16–32 px: three cubes. */
const SMALL: { x: number; y: number; color: CubeColor }[] = [
  { x: WIDTH / 2 + GAP / 2, y: 0, color: "orange" },
  { x: 0, y: 1.5 * EDGE + GAP, color: "turquoise" },
  { x: WIDTH + GAP, y: 1.5 * EDGE + GAP, color: "white" },
];
const SMALL_SIZE = { w: 2 * WIDTH + GAP, h: 3.5 * EDGE + GAP };

export function LogoMark({ size = 36, small = false, className }: { size?: number; small?: boolean; className?: string }) {
  const cubes = small ? SMALL : PYRAMID;
  const { w, h } = small ? SMALL_SIZE : PYRAMID_SIZE;
  const side = Math.max(w, h);
  const viewBox = `${((w - side) / 2).toFixed(2)} ${((h - side) / 2).toFixed(2)} ${side.toFixed(2)} ${side.toFixed(2)}`;
  return (
    <svg width={size} height={size} viewBox={viewBox} className={className} role="img" aria-label="ReadyStackGo">
      {cubes.map((c, i) => (
        <Cube key={i} {...c} />
      ))}
    </svg>
  );
}

/**
 * Mark and wordmark. `context="nav"` uses the stack color for the sidebar background,
 * `context="page"` the one for page and header backgrounds.
 */
export function Logo({ context = "page", className = "" }: { context?: "page" | "nav"; className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2.5 ${className}`}>
      <LogoMark size={36} />
      <span className="font-display text-[21px] font-extrabold leading-none tracking-[-0.3px]" aria-hidden="true">
        <span className="text-logo-ready">Ready</span>
        <span className={context === "nav" ? "text-logo-stack-on-nav" : "text-logo-stack"}>Stack</span>
        <span className="text-logo-go">Go</span>
      </span>
    </span>
  );
}

export default Logo;
