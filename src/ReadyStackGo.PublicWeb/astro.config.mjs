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
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/ams/readystackgo' }],
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
			],
			defaultLocale: 'de',
			locales: {
				de: { label: 'Deutsch', lang: 'de' },
				en: { label: 'English', lang: 'en' },
			},
			customCss: ['./src/styles/tailwind.css'],
		}),
	],
});
