// ReadyStackGo logo: three isometric cubes and the wordmark (docs/branding/logo-refresh).
// Geometry and outlines come from logoArtwork.ts, generated from the same master as the SVG files.
// Cube colors and "Ready"/"Go" are fixed brand colors; only "Stack" follows the background via the
// logo-stack theme tokens.
import { useId } from "react";
import { LOGO_MARK, LOGO_MARK_SMALL, LOGO_WORDMARK, type LogoMarkArtwork } from "./logoArtwork";

/** Unique, url()-safe id per rendered logo (several logos can be on one page). */
function useMaskId() {
  return `rsgo-joint-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}`;
}

/** The cubes; the joint between the lower cubes and the orange one is cut out with a mask. */
function MarkArtwork({ artwork, maskId }: { artwork: LogoMarkArtwork; maskId: string }) {
  return (
    <>
      <defs>
        <mask id={maskId} maskUnits="userSpaceOnUse" x={-1} y={-1} width={artwork.width + 2} height={artwork.height + 2}>
          <rect x={-1} y={-1} width={artwork.width + 2} height={artwork.height + 2} fill="#fff" />
          {artwork.joints.map((points) => (
            <polygon
              key={points}
              points={points}
              fill="#000"
              stroke="#000"
              strokeWidth={artwork.jointWidth}
              strokeLinejoin="miter"
            />
          ))}
        </mask>
      </defs>
      {artwork.cubes.map((cube, i) => (
        <g key={i} mask={cube.masked ? `url(#${maskId})` : undefined}>
          {cube.faces.map((face) => (
            <polygon key={face.points} points={face.points} fill={face.fill} />
          ))}
        </g>
      ))}
    </>
  );
}

/** The mark alone, square. `small` widens the joints for 16–32 px. */
export function LogoMark({ size = 36, small = false, className }: { size?: number; small?: boolean; className?: string }) {
  const maskId = useMaskId();
  const artwork = small ? LOGO_MARK_SMALL : LOGO_MARK;
  const side = Math.max(artwork.width, artwork.height);
  const viewBox = `${(artwork.width - side) / 2} ${(artwork.height - side) / 2} ${side} ${side}`;
  return (
    <svg width={size} height={size} viewBox={viewBox} className={className} role="img" aria-label="ReadyStackGo">
      <MarkArtwork artwork={artwork} maskId={maskId} />
    </svg>
  );
}

/**
 * Mark and wordmark. `context="nav"` uses the stack color for the sidebar background,
 * `context="page"` the one for page and header backgrounds.
 */
export function Logo({
  context = "page",
  height = 36,
  className = "",
}: {
  context?: "page" | "nav";
  height?: number;
  className?: string;
}) {
  const maskId = useMaskId();
  const width = (height * LOGO_WORDMARK.width) / LOGO_WORDMARK.height;
  return (
    <svg
      width={width}
      height={height}
      viewBox={`0 0 ${LOGO_WORDMARK.width} ${LOGO_WORDMARK.height}`}
      className={className}
      role="img"
      aria-label="ReadyStackGo"
    >
      <MarkArtwork artwork={LOGO_MARK} maskId={maskId} />
      <path d={LOGO_WORDMARK.ready} fill={LOGO_WORDMARK.colors.ready} />
      <path
        d={LOGO_WORDMARK.stack}
        fill="currentColor"
        className={context === "nav" ? "text-logo-stack-on-nav" : "text-logo-stack"}
      />
      <path d={LOGO_WORDMARK.go} fill={LOGO_WORDMARK.colors.go} />
    </svg>
  );
}

export default Logo;
