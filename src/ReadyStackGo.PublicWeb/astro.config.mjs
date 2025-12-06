// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import react from '@astrojs/react';
import tailwindcss from '@tailwindcss/vite';

// https://astro.build/config
export default defineConfig({
	site: 'http://localhost:8888',
	vite: {
		plugins: [tailwindcss()],
	},
	integrations: [
		react(),
		starlight({
			title: 'ReadyStackGo',
			head: [
				{
					tag: 'script',
					content: `
						// Sync theme from Landing Page before Starlight loads
						(function() {
							const landingTheme = localStorage.getItem('theme');
							if (landingTheme) {
								localStorage.setItem('starlight-theme', landingTheme);
							}
						})();

						document.addEventListener('DOMContentLoaded', function() {
							const siteTitle = document.querySelector('.site-title');
							if (siteTitle) {
								siteTitle.href = '/';
							}
						});
					`,
				},
			],
			social: [],
			sidebar: [
				{ label: 'Willkommen', slug: 'index', translations: { en: 'Welcome' } },
				{ label: 'Release Notes', slug: 'release-notes' },
				{ label: 'Einführung', slug: 'introduction', translations: { en: 'Introduction' } },
				{
					label: 'Erste Schritte',
					translations: { en: 'Getting Started' },
					items: [
						{ label: 'Schnellstart', slug: 'getting-started/quickstart', translations: { en: 'Quickstart' } },
						{
							label: 'Installation',
							items: [
								{ label: 'Übersicht', slug: 'getting-started/installation', translations: { en: 'Overview' } },
								{ label: 'Bootstrap Script', slug: 'getting-started/installation/script' },
								{ label: 'Docker Run', slug: 'getting-started/installation/docker-run' },
								{ label: 'Docker Compose', slug: 'getting-started/installation/docker-compose' },
							],
						},
						{ label: 'Ersteinrichtung', slug: 'getting-started/initial-setup', translations: { en: 'Initial Setup' } },
						{ label: 'Ersten Stack deployen', slug: 'getting-started/first-deployment', translations: { en: 'First Deployment' } },
					],
				},
				{
					label: 'Dokumentation',
					translations: { en: 'Documentation' },
					autogenerate: { directory: 'docs' },
				},
				{
					label: 'Referenz',
					translations: { en: 'Reference' },
					items: [
						{ label: 'RSGo Manifest Format', slug: 'reference/manifest-format' },
						{ label: 'Variable Types', slug: 'reference/variable-types', translations: { de: 'Variablentypen' } },
					],
				},
				{ label: 'Lizenzen', slug: 'licenses', translations: { en: 'Licenses' } },
			],
			defaultLocale: 'de',
			locales: {
				de: { label: 'Deutsch', lang: 'de' },
				en: { label: 'English', lang: 'en' },
			},
			customCss: ['./src/styles/starlight.css'],
		}),
	],
});
