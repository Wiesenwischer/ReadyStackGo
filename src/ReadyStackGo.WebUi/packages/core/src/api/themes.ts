import { apiGet } from './client';

/** A theme package offered by this installation (see docs/Architecture/Themes.md). */
export interface ThemeSummary {
  id: string;
  name: string;
  description: string;
  cssUrl: string;
}

export interface ListThemesResponse {
  /** Null when the installation offers no theme. */
  default: string | null;
  themes: ThemeSummary[];
}

export async function listThemes(): Promise<ListThemesResponse> {
  return apiGet<ListThemesResponse>('/api/themes');
}
