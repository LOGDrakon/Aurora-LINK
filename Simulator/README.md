# Aurora-LINK Device Simulator

Simulateur Python d'un module Aurora LED, parlant le protocole LINK sur port série virtuel.

L'appareil démarre **verrouillé**. Une fois l'authentification réussie (`AUTH`), le simulateur envoie une frame `GETINPUT` initiale avec l'état des entrées. Chaque changement d'entrée via la console pousse une nouvelle frame `GETINPUT`.

## Prérequis

```bash
pip install pyserial
```

Pour créer une paire de ports COM virtuels :
- **Windows** : [com0com](https://sourceforge.net/projects/com0com/) (ex. COM10 ↔ COM11)
- **Linux/macOS** : `socat -d -d pty,raw,echo=0 pty,raw,echo=0`

## Utilisation

Le simulateur se connecte à un port COM ; Aurora-LINK se connecte à l'autre.

```bash
# Lancement par défaut (COM10, 115200, mot de passe "aurora")
python aurora_link_simulator.py

# Personnalisé
python aurora_link_simulator.py --port COM11 --password secret
```

## Commandes interactives

Une fois lancé, le simulateur affiche un prompt `aurora-sim>` :

| Commande | Description |
|----------|-------------|
| `I0=ON` | Met l'entrée I0 à ON |
| `I3=OFF` | Met l'entrée I3 à OFF |
| `I7=1` | Met l'entrée I7 à ON (forme numérique) |
| `I2=0` | Met l'entrée I2 à OFF (forme numérique) |
| `STATUS` | Affiche l'état de toutes les entrées |
| `QUIT` | Arrête le simulateur |

Chaque changement d'entrée envoie immédiatement une frame `GETINPUT` au client (uniquement si connecté).

## Protocole LINK supporté

**Frames reçues (depuis Aurora-LINK) :**
- `LINK:GETAPP\0` → répond avec l'app-id `AURORA`
- `LINK:AURORA:GETV\0` → répond avec les infos device (LOCKED=true)
- `LINK:AURORA:AUTH:<password>\0` → authentification, puis envoi initial de `GETINPUT`
- `LINK:AURORA:PING\0` → PONG
- `LINK:AURORA:UPLOAD:START:<size>\0` → prépare la réception d'un programme .flora, répond OK
- `LINK:AURORA:UPLOAD:DATA:<seq>:<hex>\0` → reçoit un paquet de données (max 64 bytes bruts encodés en hex), répond OK
- `LINK:AURORA:UPLOAD:END\0` → vérifie l'intégrité du programme reçu (taille + CRC-32 + signature FLOR), répond OK ou ERR

**Frames poussées (vers Aurora-LINK) :**
- `LINK:AURORA:GETINPUT:<payload>\0` — payload = 10 caractères `0`/`1` (I0..I9)
  - Envoyé 1 fois après authentification réussie
  - Envoyé à chaque changement d'entrée via la console

## Options

```
--port       Port série (défaut: COM10)
--baud       Baud rate (défaut: 115200)
--password   Mot de passe AUTH (défaut: aurora)
--model      Nom du modèle (défaut: Aurora-LED)
--uid        UID de l'appareil (défaut: 0xAUR00001)
--quiet      Désactiver les logs de frames
```
