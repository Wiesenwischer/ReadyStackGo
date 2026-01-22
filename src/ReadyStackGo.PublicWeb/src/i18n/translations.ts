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
			'ReadyStackGo macht Container-Deployments einfach. Nutze das leistungsstarke RSGo Manifest Format oder importiere bestehende Docker Compose Dateien.',
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
		'features.multistack.title': 'Multi-Stack Produkte',
		'features.multistack.desc': 'Definiere komplexe Produkte mit mehreren Stacks und gemeinsamen Variablen in einem Manifest.',
		'features.editors.title': 'Intelligente Editoren',
		'features.editors.desc': 'Spezialisierte UI-Editoren für Variablentypen wie Connection Strings, Ports und Passwörter.',
		'features.manifest.title': 'RSGo Manifest Format',
		'features.manifest.desc': 'Leistungsstarkes Stack-Format mit Typisierung, Validierung und Docker Compose Import.',
		'features.health.title': 'Health Monitoring',
		'features.health.desc': 'Echtzeit-Überwachung aller Container und Services mit automatischen Statusaktualisierungen.',
		'features.maintenance.title': 'Wartungsmodus',
		'features.maintenance.desc': 'Kontrollierte Wartungsfenster mit automatischem Container-Lifecycle-Management.',
		'features.tls.title': 'TLS & Zertifikate',
		'features.tls.desc': 'Flexibles HTTPS-Management mit eigenen Zertifikaten, Let\'s Encrypt und Reverse Proxy-Unterstützung.',

		// Feature Pages
		'featurepage.multistack.subtitle': 'Definiere komplexe Anwendungen mit mehreren Docker Stacks und gemeinsamen Variablen in einem einzigen Manifest.',
		'featurepage.editors.subtitle': 'Spezialisierte UI-Komponenten für verschiedene Variablentypen - von Connection Strings bis Port-Konfigurationen.',
		'featurepage.manifest.subtitle': 'Ein flexibles, typisiertes Format für Stack-Definitionen mit Validierung, Metadaten und Docker Compose Kompatibilität.',
		'featurepage.health.subtitle': 'Überwache den Zustand deiner Container und Services in Echtzeit mit automatischen Benachrichtigungen.',
		'featurepage.maintenance.subtitle': 'Verwalte Wartungsfenster mit automatischem Stoppen und Starten von Containern.',
		'featurepage.tls.subtitle': 'Verwalte HTTPS-Zertifikate flexibel - von selbstsignierten über eigene Zertifikate bis hin zu Let\'s Encrypt mit automatischer Erneuerung.',

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
			'ReadyStackGo makes container deployments easy. Use the powerful RSGo Manifest format or import existing Docker Compose files.',
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
		'features.multistack.title': 'Multi-Stack Products',
		'features.multistack.desc': 'Define complex products with multiple stacks and shared variables in one manifest.',
		'features.editors.title': 'Smart Editors',
		'features.editors.desc': 'Specialized UI editors for variable types like connection strings, ports and passwords.',
		'features.manifest.title': 'RSGo Manifest Format',
		'features.manifest.desc': 'Powerful stack format with type validation, rich metadata and Docker Compose import.',
		'features.health.title': 'Health Monitoring',
		'features.health.desc': 'Real-time monitoring of all containers and services with automatic status updates.',
		'features.maintenance.title': 'Maintenance Mode',
		'features.maintenance.desc': 'Controlled maintenance windows with automatic container lifecycle management.',
		'features.tls.title': 'TLS & Certificates',
		'features.tls.desc': 'Flexible HTTPS management with custom certificates, Let\'s Encrypt and reverse proxy support.',

		// Feature Pages
		'featurepage.multistack.subtitle': 'Define complex applications with multiple Docker Stacks and shared variables in a single manifest.',
		'featurepage.editors.subtitle': 'Specialized UI components for different variable types - from connection strings to port configurations.',
		'featurepage.manifest.subtitle': 'A flexible, typed format for stack definitions with validation, metadata and Docker Compose compatibility.',
		'featurepage.health.subtitle': 'Monitor the health of your containers and services in real-time with automatic notifications.',
		'featurepage.maintenance.subtitle': 'Manage maintenance windows with automatic container stop and start.',
		'featurepage.tls.subtitle': 'Manage HTTPS certificates flexibly - from self-signed through custom certificates to Let\'s Encrypt with automatic renewal.',

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
	return `/${lang}/docs/`;
}
