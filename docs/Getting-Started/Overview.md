# Getting Started - Übersicht

ReadyStackGo ist eine selbst gehostete Plattform zur einfachen Installation, Aktualisierung und Verwaltung komplexer Microservice-Stacks auf Basis von Docker.

## Was ist ReadyStackGo?

ReadyStackGo (RSGO) löst das Problem der komplexen Bereitstellung von Microservice-Architekturen in On-Premise-Umgebungen. Statt manueller Docker-Commands, komplexer Compose-Dateien oder Kubernetes-Konfigurationen erhalten Sie:

- ✅ **Einen einzigen Admin-Container**, der den gesamten Stack verwaltet
- ✅ **Einen geführten Setup-Wizard** für die Erstinstallation
- ✅ **Eine moderne Web-UI** zur Verwaltung
- ✅ **Manifest-basierte Deployments** für reproduzierbare Installationen

## Für wen ist ReadyStackGo?

### On-Premise-Kunden
Kunden, die Software in ihren eigenen Rechenzentren betreiben und eine einfache Installationslösung benötigen.

### Partner & Reseller
Partner, die Software-Stacks für ihre Kunden installieren und warten müssen.

### Entwicklungsteams
Teams, die identische Umgebungen für Entwicklung, Testing und Produktion benötigen.

### Edge & IoT
Installationen in isolierten oder eingeschränkten Netzwerkumgebungen.

## Kernkonzepte

### Admin-Container
Der zentrale Container, der alle anderen Container verwaltet. Er ist der einzige Container, den Sie manuell starten müssen.

### Manifeste
Deklarative Beschreibungen des gewünschten Zustands eines Software-Stacks. Manifeste definieren, welche Container in welchen Versionen deployt werden sollen.

### Setup-Wizard
Ein geführter Installationsassistent, der die Ersteinrichtung vereinfacht (Admin-Benutzer, Organisation, Verbindungen, Installation).

### Feature Flags
Kontextübergreifende Schalter zur Aktivierung/Deaktivierung fachlicher Funktionen.

## Technische Voraussetzungen

- **Docker Engine** (Version 20.10 oder höher)
- **Linux, Windows oder macOS** mit Docker-Unterstützung
- **Zugriff auf Docker Socket** (`/var/run/docker.sock`)
- **Ports**: 8080 (HTTP), 8443 (HTTPS)
- Optional: **Eigenes TLS-Zertifikat** (sonst Self-Signed)

## Nächste Schritte

1. [Quick Start](Quick-Start.md) - Schnellstart-Anleitung
2. [Installation](Installation.md) - Detaillierte Installationsanleitung
3. [Setup Wizard](../Setup-Wizard/Wizard-Flow.md) - Wizard-Durchlauf

## Hilfreiche Ressourcen

- [Architecture Overview](../Architecture/Overview.md) - Systemarchitektur
- [Configuration](../Configuration/Config-Files.md) - Konfigurationsdateien
- [Security](../Security/Overview.md) - Sicherheitskonzepte
