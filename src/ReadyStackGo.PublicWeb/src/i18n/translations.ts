export const languages = {
	de: 'Deutsch',
	en: 'English',
} as const;

export const defaultLang = 'de';

export type Lang = keyof typeof languages;

export const translations = {
	de: {
		// Meta
		'meta.title': 'ReadyStackGo - Docker Stack Deployment leicht gemacht',
		'meta.description': 'Self-hosted Container Management Platform',

		// Header
		'header.features': 'Features',
		'header.installation': 'Installation',
		'header.docs': 'Dokumentation',
		'header.cta': 'Jetzt starten',

		// Hero
		'hero.badge': 'Self-Hosted Docker Management',
		'hero.title1': 'Docker Stacks deployen',
		'hero.title2': ' mit einem Klick',
		'hero.subtitle':
			'ReadyStackGo macht Docker Compose Deployments einfach. Installiere mit einem Befehl, verwalte Stacks über eine intuitive Web-Oberfläche.',
		'hero.cta.install': 'Installation starten',
		'hero.cta.docs': 'Dokumentation',
		'hero.quickinstall': 'Schnellinstallation:',

		// Features
		'features.title': 'Warum ReadyStackGo?',
		'features.subtitle':
			'Eine moderne Plattform für Container-Management, entwickelt für Einfachheit und Kontrolle.',
		'features.selfhosted.title': 'Self-Hosted',
		'features.selfhosted.desc':
			'Volle Kontrolle über deine Daten. Läuft auf deiner eigenen Infrastruktur ohne externe Abhängigkeiten.',
		'features.oneclick.title': 'Ein-Klick Deployment',
		'features.oneclick.desc':
			'Deploye komplexe Container-Stacks mit einem einzigen Klick aus vorgefertigten Templates.',
		'features.multienv.title': 'Multi-Environment',
		'features.multienv.desc':
			'Verwalte Development, Staging und Production Umgebungen von einer zentralen Oberfläche.',
		'features.webui.title': 'Web UI',
		'features.webui.desc':
			'Intuitive Weboberfläche für Stack-Management, Monitoring und Konfiguration.',
		'features.git.title': 'Git Integration',
		'features.git.desc': 'Lade Stack-Definitionen direkt aus Git-Repositories. Automatische Updates möglich.',
		'features.logs.title': 'Echtzeit Logs',
		'features.logs.desc': 'Verfolge Container-Logs in Echtzeit für schnelles Debugging und Monitoring.',

		// Installation
		'install.title': 'In Sekunden einsatzbereit',
		'install.subtitle':
			'ReadyStackGo ist in wenigen Minuten installiert. Wähle deine bevorzugte Methode.',
		'install.script.title': 'Bootstrap Script',
		'install.script.desc': 'Automatische Installation mit einem Befehl',
		'install.script.recommended': 'Empfohlen',
		'install.docker.title': 'Docker Compose',
		'install.docker.desc': 'Für bestehende Docker-Umgebungen',
		'install.copy': 'Kopieren',
		'install.copied': 'Kopiert!',
		'install.more': 'Weitere Installationsoptionen',

		// Footer
		'footer.product': 'Produkt',
		'footer.features': 'Features',
		'footer.installation': 'Installation',
		'footer.docs': 'Dokumentation',
		'footer.resources': 'Ressourcen',
		'footer.github': 'GitHub',
		'footer.releases': 'Releases',
		'footer.issues': 'Issues',
		'footer.legal': 'Rechtliches',
		'footer.privacy': 'Datenschutz',
		'footer.imprint': 'Impressum',
		'footer.copyright': 'Alle Rechte vorbehalten.',
	},
	en: {
		// Meta
		'meta.title': 'ReadyStackGo - Docker Stack Deployment Made Easy',
		'meta.description': 'Self-hosted Container Management Platform',

		// Header
		'header.features': 'Features',
		'header.installation': 'Installation',
		'header.docs': 'Documentation',
		'header.cta': 'Get Started',

		// Hero
		'hero.badge': 'Self-Hosted Docker Management',
		'hero.title1': 'Deploy Docker Stacks',
		'hero.title2': ' with One Click',
		'hero.subtitle':
			'ReadyStackGo makes Docker Compose deployments easy. Install with one command, manage stacks through an intuitive web interface.',
		'hero.cta.install': 'Start Installation',
		'hero.cta.docs': 'Documentation',
		'hero.quickinstall': 'Quick Install:',

		// Features
		'features.title': 'Why ReadyStackGo?',
		'features.subtitle':
			'A modern platform for container management, built for simplicity and control.',
		'features.selfhosted.title': 'Self-Hosted',
		'features.selfhosted.desc':
			'Full control over your data. Runs on your own infrastructure without external dependencies.',
		'features.oneclick.title': 'One-Click Deployment',
		'features.oneclick.desc':
			'Deploy complex container stacks with a single click using pre-built templates.',
		'features.multienv.title': 'Multi-Environment',
		'features.multienv.desc':
			'Manage Development, Staging and Production environments from a single interface.',
		'features.webui.title': 'Web UI',
		'features.webui.desc':
			'Intuitive web interface for stack management, monitoring and configuration.',
		'features.git.title': 'Git Integration',
		'features.git.desc': 'Load stack definitions directly from Git repositories. Automatic updates available.',
		'features.logs.title': 'Real-time Logs',
		'features.logs.desc': 'Follow container logs in real-time for quick debugging and monitoring.',

		// Installation
		'install.title': 'Ready in Seconds',
		'install.subtitle':
			'ReadyStackGo installs in minutes. Choose your preferred method.',
		'install.script.title': 'Bootstrap Script',
		'install.script.desc': 'Automatic installation with one command',
		'install.script.recommended': 'Recommended',
		'install.docker.title': 'Docker Compose',
		'install.docker.desc': 'For existing Docker environments',
		'install.copy': 'Copy',
		'install.copied': 'Copied!',
		'install.more': 'More installation options',

		// Footer
		'footer.product': 'Product',
		'footer.features': 'Features',
		'footer.installation': 'Installation',
		'footer.docs': 'Documentation',
		'footer.resources': 'Resources',
		'footer.github': 'GitHub',
		'footer.releases': 'Releases',
		'footer.issues': 'Issues',
		'footer.legal': 'Legal',
		'footer.privacy': 'Privacy',
		'footer.imprint': 'Imprint',
		'footer.copyright': 'All rights reserved.',
	},
} as const;

export function t(lang: Lang, key: keyof (typeof translations)['de']): string {
	return translations[lang][key] || translations.de[key] || key;
}

export function getDocsPath(lang: Lang): string {
	return `/${lang}/`;
}
