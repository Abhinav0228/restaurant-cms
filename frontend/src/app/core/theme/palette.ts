import { storage } from '@core/storage/storage';

export type Palette = 'navy-pink' | 'charcoal-blue' | 'cream-orange';

/** The three palettes from the colour brief: two neutrals that get along plus one accent. */
export const PALETTES: { value: Palette; label: string; swatches: [string, string, string] }[] = [
  { value: 'navy-pink', label: 'Navy and white, hot pink', swatches: ['#0F1B3D', '#FFFFFF', '#FF3D8B'] },
  { value: 'charcoal-blue', label: 'Charcoal and sand, electric blue', swatches: ['#1E1E1C', '#D8CBB3', '#2F6BFF'] },
  { value: 'cream-orange', label: 'Cream and ink, burnt orange', swatches: ['#F3EEE3', '#111111', '#D9541E'] },
];

export const DEFAULT_PALETTE: Palette = 'navy-pink';
const KEY = 'rcms.palette';

export function isPalette(v: unknown): v is Palette {
  return PALETTES.some((p) => p.value === v);
}

/** Applies a palette to the whole app and remembers it so the next load paints correctly at once. */
export function applyPalette(palette: string | null | undefined) {
  const value = isPalette(palette) ? palette : DEFAULT_PALETTE;
  document.documentElement.dataset['palette'] = value;
  storage.set(KEY, value);
  syncThemeColor();
}

/** Keeps the mobile browser bar the same colour as the page background. */
export function syncThemeColor() {
  const bg = getComputedStyle(document.documentElement).getPropertyValue('--cream').trim();
  if (bg) document.querySelector('meta[name="theme-color"]')?.setAttribute('content', bg);
}
