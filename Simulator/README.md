# Aurora-LINK Device Simulator

Simulateur Python d'un module Aurora LED, parlant le protocole LINK sur port série virtuel.

L'appareil démarre **verrouillé**. L'authentification utilise un mécanisme **challenge-response** :

1. `GETV` annonce `HASH=SHA256` et `LOCKED=true`
2. Le client envoie `AUTH_INIT\x1f<clientNonce>` — le simulateur répond avec `<deviceNonce>`
3. Le client envoie `AUTH\x1f<hex_digest>` où `digest = HASH(clientNonce + deviceNonce + HASH(password))`

Le mot de passe n'est **jamais transmis en clair**. L'appareil ne stocke que `HASH(password)`, jamais le mot de passe en clair. Les nonces aléatoires empêchent les attaques par rejeu.

Une fois authentifié, le simulateur envoie une frame `GETINPUT` initiale. Chaque changement d'entrée via la console pousse une nouvelle frame `GETINPUT`.

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
# Lancement par défaut (COM10, 115200, mot de passe "aurora", SHA256)
python aurora_link_simulator.py

# Personnalisé
python aurora_link_simulator.py --port COM11 --password secret --hash SHA512
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

Séparateur de champs : `\x1f` (Unit Separator, ASCII 31) — LINK v2.0

**Frames reçues (depuis Aurora-LINK) :**
- `LINK\x1fGETAPP\0` → répond avec l'app-id `AURORA`
- `LINK\x1fAURORA\x1fGETV\0` → répond avec les infos device (`HASH=SHA256`, `LOCKED=true`)
- `LINK\x1fAURORA\x1fAUTH_INIT\x1f<clientNonce>\0` → échange de nonces, répond avec `<deviceNonce>`
- `LINK\x1fAURORA\x1fAUTH\x1f<hashedPassword>\0` → vérification challenge-response, puis envoi initial de `GETINPUT`
- `LINK\x1fAURORA\x1fCHPASSWD\x1f<hashedOld>\x1f<encryptedNew>\0` → changement de mot de passe (voir ci-dessous)
- `LINK\x1fAURORA\x1fPING\0` → PONG
- `LINK\x1fAURORA\x1fUPLOAD\x1fSTART\x1f<size>\0` → prépare la réception d'un programme .flora, répond OK
- `LINK\x1fAURORA\x1fUPLOAD\x1fDATA\x1f<seq>\x1f<hex>\0` → reçoit un paquet de données, répond OK
- `LINK\x1fAURORA\x1fUPLOAD\x1fEND\0` → vérifie l'intégrité du programme reçu (taille + CRC-32 + signature FLOR), répond OK ou ERR
- `LINK\x1fAURORA\x1fDONE\0` → signale la fin de l'échange, répond OK

**Frames poussées (vers Aurora-LINK) :**
- `LINK\x1fAURORA\x1fGETINPUT\x1f<payload>\0` — payload = 10 caractères `0`/`1` (I0..I9)
  - Envoyé 1 fois après authentification réussie
  - Envoyé à chaque changement d'entrée via la console

## Changement de mot de passe (`CHPASSWD`)

L'appareil doit être **déverrouillé** (authentifié). Le protocole utilise le modèle de **double hash** : ni le mot de passe actuel, ni le nouveau ne transitent sur le lien — même sous forme chiffrée.

1. Le client effectue un `AUTH_INIT` pour obtenir des nonces frais (`clientNonce`, `deviceNonce`)
2. Le client envoie `CHPASSWD\x1f<hashedOld>\x1f<encryptedNewHash>` où :
   - `hashedOld = HASH(clientNonce + deviceNonce + HASH(ancienMotDePasse))` — vérification
   - `encryptedNewHash = HEX(XOR(HASH(nouveauMotDePasse)_bytes, clé_chiffrement))`
   - `clé_chiffrement = HASH(deviceNonce + clientNonce + HASH(ancienMotDePasse))` — l'ordre des nonces est inversé pour que la clé diffère du hash de vérification
3. Le simulateur vérifie `hashedOld`, déchiffre `HASH(nouveauMotDePasse)` par XOR, et le stocke comme nouveau `password_hash`

**Sécurité :** le device ne stocke et ne manipule que des hash de mots de passe. Il ne peut jamais connaître le mot de passe en clair. La clé de chiffrement n'est jamais envoyée sur le lien.

**Réponses :**
- `OK` — mot de passe modifié avec succès
- `ERR\x1fLOCKED` — l'appareil est verrouillé
- `ERR\x1fINVALID_PASSWORD` — l'ancien mot de passe est incorrect
- `ERR\x1fNO_AUTH_INIT` — aucun échange de nonces en cours
- `ERR\x1fWEAK_PASSWORD` — le nouveau mot de passe est vide

## Options

```
--port             Port série (défaut: COM10)
--baud             Baud rate (défaut: 115200)
--password         Mot de passe AUTH (défaut: aurora)
--model            Nom du modèle (défaut: Aurora-LED)
--uid              UID de l'appareil (défaut: 0xAUR00001)
--hash             Algorithme de hash (défaut: SHA256, choix: SHA1/SHA256/SHA384/SHA512)
--max-packet-size  Taille max des paquets TX en bytes, 0=pas de découpage (défaut: 64)
--quiet            Désactiver les logs de frames
```
