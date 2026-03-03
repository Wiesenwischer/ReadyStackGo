/** Convert PascalCase phase names to readable form: "PullingImages" -> "Pulling Images" */
export const formatPhase = (phase: string | undefined): string => {
  if (!phase) return '';
  return phase.replace(/([A-Z])/g, ' $1').trim();
};
