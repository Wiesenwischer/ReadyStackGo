# Remote Environments — Übersicht

Dieser Plan wurde in drei separate Pläne aufgeteilt (ein Plan je Remote-Typ):

| Phase | Plan | Typ | Status |
|-------|------|-----|--------|
| Phase 1 | [PLAN-ssh-tunnel.md](PLAN-ssh-tunnel.md) | SSH Tunnel (Controller → SSH → Docker) | Offen |
| Phase 2 | [PLAN-docker-tcp.md](PLAN-docker-tcp.md) | Docker TCP/TLS (Controller → Docker API) | Offen |
| Phase 3–4 | [PLAN-remote-agent.md](PLAN-remote-agent.md) | Remote Agent (Agent → Controller, Pull-Modell) | Offen |

## Reihenfolge

1. **SSH Tunnel** — Häufigster Use Case, geringste Infrastruktur-Anforderungen
2. **Docker TCP/TLS** — Baut auf ConnectionConfig-Polymorphismus aus Phase 1 auf
3. **Remote Agent** — Komplexester Typ, RSGO-zu-RSGO Konnektivität
