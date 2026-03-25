# Documentation Technique Aurora — Index

**Date :** 2025-07  
**Projet :** Aurora-LINK (module LED sur STM32 + logiciel PC WinUI 3)

---

## Documents techniques

| Référence             | Titre                             | Contenu principal                                          |
|------------------------|-----------------------------------|------------------------------------------------------------|
| [DT-AURORA-PROTO-001](DT-AURORA-PROTO-001.md) | Protocole de communication | Format des trames LINK, séparateurs, commandes supportées, diagramme de séquence, transport série, fragmentation USB |
| [DT-AURORA-AUTH-001](DT-AURORA-AUTH-001.md)   | Authentification et sécurité | AUTH_INIT (nonces), AUTH (challenge-response), CHPASSWD (changement de mot de passe avec XOR), stockage des hash |
| [DT-AURORA-MEM-001](DT-AURORA-MEM-001.md)    | Format mémoire .flora       | Structure binaire Header + TLV (LEDs, Scènes, Entrées, Système) + CRC-32 + Signature, validation, structures C |
| [DT-AURORA-UPLOAD-001](DT-AURORA-UPLOAD-001.md) | Protocole de téléversement | Phases START/DATA/END, fragmentation par paquets, vérification d'intégrité, écriture Flash |
| [DT-AURORA-IO-001](DT-AURORA-IO-001.md)      | Entrées/Sorties et GETINPUT | 10 entrées GPIO, trame GETINPUT poussée, anti-rebond, triggers, actions, implémentation polling |

---

## Architecture rapide

```
┌─────────────────────────────────────────────────────────┐
│                   Aurora-LINK (PC, WinUI 3)              │
│                                                         │
│  ConnectionDialog ──→ AUTH_INIT + AUTH                   │
│  DashboardPage    ←── GETINPUT (push)                   │
│  SystemPage       ──→ CHPASSWD                          │
│  MainWindow       ──→ UPLOAD START/DATA/END             │
│                                                         │
│  AuroraConfigSerializer ──→ .flora (binaire)            │
│  AuroraProjectService   ──→ .ora  (JSON)                │
└────────────────────┬────────────────────────────────────┘
                     │  USB CDC / Virtual COM
                     │  LINK v2.0 (115200 8N1)
                     │  Séparateur: \x1F  Fin: \0
┌────────────────────┴────────────────────────────────────┐
│                Module Aurora (STM32G0B1)                  │
│                                                         │
│  LINK Parser ──→ Dispatcher de commandes                 │
│  Auth Manager ──→ Nonces + HASH(password) en Flash       │
│  Config Store ──→ Page Flash 2048 bytes (.flora)         │
│  LED Driver   ──→ PWM RGB (2 LEDs synchrones)            │
│  GPIO Inputs  ──→ 10 entrées avec anti-rebond            │
└─────────────────────────────────────────────────────────┘
```

---

## Flux de travail typique

1. **Découverte** — Le PC scanne les ports série, envoie `GETAPP` + `GETV` pour détecter les modules Aurora.
2. **Authentification** — `AUTH_INIT` (nonces) puis `AUTH` (challenge-response SHA-256).
3. **Connexion** — Le device envoie `GETINPUT` initial. Le Dashboard s'affiche.
4. **Configuration** — L'utilisateur crée/modifie un projet `.ora` (scènes, entrées, système).
5. **Validation** — Vérification des règles DT-AURORA-MEM-001 avant export.
6. **Export** — Sérialisation en `.flora` (binaire avec CRC-32 + signature FLOR).
7. **Téléversement** — `UPLOAD START` → `UPLOAD DATA` × N → `UPLOAD END`.
8. **Monitoring** — Réception continue des `GETINPUT` sur changement d'état des entrées.

---

## Implémentation STM32 — Checklist

### Commandes à implémenter

- [ ] Parser de trames LINK (séparateur `\x1F`, terminateur `\0`)
- [ ] `GETAPP` → répondre `AURORA`
- [ ] `GETV` → répondre avec version, UID, model, hash method, locked state
- [ ] `AUTH_INIT` → générer nonce device, stocker les deux nonces
- [ ] `AUTH` → vérifier `HASH(cN + dN + storedHash)`, déverrouiller si OK
- [ ] `CHPASSWD` → vérifier ancien mdp, déchiffrer XOR, stocker nouveau hash
- [ ] `PING` → répondre `PONG`
- [ ] `UPLOAD START` → préparer buffer réception
- [ ] `UPLOAD DATA` → accumuler paquets séquentiels
- [ ] `UPLOAD END` → vérifier taille + signature + CRC-32, écrire en Flash
- [ ] `DONE` → répondre `OK`
- [ ] Commande inconnue → répondre `ERR UNKNOWN_COMMAND`

### Fonctionnalités

- [ ] Stockage `HASH(password)` en Flash (page dédiée)
- [ ] Lecture/écriture configuration `.flora` en Flash (page 2048 bytes)
- [ ] CRC-32 IEEE 802.3
- [ ] Polling GPIO 10 entrées avec anti-rebond 20 ms
- [ ] Push `GETINPUT` sur changement d'état
- [ ] Pilotage PWM RGB (modes: Off, Static, Blink, Fade, Burst, Double)
- [ ] Fragmentation USB (envoi par paquets de 64 bytes max)
- [ ] RNG hardware pour génération des nonces
- [ ] SHA-256 (minimum) pour authentification

---

© 2025 — Projet Aurora-LINK
