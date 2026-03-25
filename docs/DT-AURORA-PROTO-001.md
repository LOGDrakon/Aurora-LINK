# DT-AURORA-PROTO-001 — Protocole de communication AURORA sur LINK

**Révision :** A  
**Date :** 2025-07  
**Portée :** Firmware STM32 + Application PC Aurora-LINK  

---

## 1. Introduction

Le module Aurora communique avec le logiciel PC via le protocole **LINK v2.0** sur liaison série USB (CDC ou Virtual COM Port). Ce document décrit la couche applicative AURORA implémentée au-dessus de LINK.

L'identifiant d'application est : **`AURORA`**

---

## 2. Couche transport

| Paramètre       | Valeur                  |
|------------------|-------------------------|
| Interface        | USB CDC / Virtual COM   |
| Baud rate        | 115 200                 |
| Data bits        | 8                       |
| Parity           | None                    |
| Stop bits        | 1                       |
| Max packet size  | 64 bytes (USB FS)       |

### 2.1 Fragmentation

Le buffer matériel USB Full Speed du STM32 est limité à **64 octets**. Les trames LINK dépassant cette taille sont automatiquement découpées en paquets de 64 octets maximum par la couche transport.

- **Émission :** découper la trame en chunks de `MaxPacketSize` avant écriture sur le port.
- **Réception :** accumuler les octets entrants dans un buffer jusqu'au délimiteur `\0`.

Aucun en-tête de fragmentation n'est nécessaire — le protocole utilise `\0` comme délimiteur de fin de trame.

---

## 3. Format des trames LINK

### 3.1 Séparateur de champs

`\x1F` — **Unit Separator** (ASCII 31)

### 3.2 Délimiteur de fin de trame

`\0` — **Null** (ASCII 0)

### 3.3 Structure générale

```
LINK\x1f[APP-ID]\x1f[COMMAND]\x1f[ARG_0]\x1f[ARG_1]\x1f...\x1f[ARG_n]\0
```

### 3.4 Trame de réponse (RETURN)

Toute réponse du device suit le format :

```
LINK\x1fAURORA\x1fRETURN\x1f[COMMAND]\x1f[ARG_0]\x1f...\x1f[ARG_n]\0
```

Le champ `COMMAND` indique la commande à laquelle la réponse se rapporte.

### 3.5 Trame poussée (PUSH)

Le device peut émettre des trames spontanées (non sollicitées) :

```
LINK\x1fAURORA\x1f[COMMAND]\x1f[ARG_0]\x1f...\0
```

Exemple : `GETINPUT` (voir DT-AURORA-IO-001).

---

## 4. Commandes supportées

### 4.1 Commandes standard LINK

| Commande   | Direction      | Description                          | Auth requise |
|------------|----------------|--------------------------------------|--------------|
| `GETAPP`   | PC → Device    | Récupère l'APP-ID du device          | Non          |
| `GETV`     | PC → Device    | Récupère les infos device + LINK     | Non          |

### 4.2 Commandes AURORA

| Commande    | Direction      | Description                                     | Auth requise |
|-------------|----------------|--------------------------------------------------|--------------|
| `AUTH_INIT` | PC → Device    | Échange de nonces pour authentification          | Non          |
| `AUTH`      | PC → Device    | Authentification par challenge-response           | Non          |
| `CHPASSWD`  | PC → Device    | Changement de mot de passe                       | Oui          |
| `PING`      | PC → Device    | Test de connectivité                             | Non          |
| `UPLOAD`    | PC → Device    | Téléversement de programme .flora (sous-cmds)    | Oui          |
| `DONE`      | PC → Device    | Signale la fin de l'échange                      | Non          |
| `GETINPUT`  | Device → PC    | Notification d'état des entrées (poussée)        | —            |

---

## 5. Détail des commandes

### 5.1 GETAPP

Récupère l'identifiant d'application.

```
→ LINK\x1fGETAPP\0
← LINK\x1fAURORA\x1fRETURN\x1fGETAPP\x1fAURORA\0
```

> **Note :** Cette commande n'a pas d'APP-ID dans la requête (commande globale LINK).

### 5.2 GETV

Récupère les informations du device.

```
→ LINK\x1fAURORA\x1fGETV\0
← LINK\x1fAURORA\x1fRETURN\x1fGETV\x1fLINKv2.0\x1fUID=0xAUR00001\x1fMODEL=Aurora-LED\x1fENC=NONE\x1fHASH=SHA256\x1fLOCKED=true\0
```

**Champs de la réponse :**

| Champ    | Format              | Description                               |
|----------|---------------------|-------------------------------------------|
| Version  | `LINKv2.0`          | Version du protocole LINK                 |
| UID      | `UID=<hex>`         | Identifiant unique du device (STM32 UID)  |
| MODEL    | `MODEL=<string>`    | Nom du modèle hardware                    |
| ENC      | `ENC=<mode>`        | Mode de chiffrement (`NONE`, `AES128`)    |
| HASH     | `HASH=<algo>`       | Algorithme de hash (`SHA256`, `SHA512`…)  |
| LOCKED   | `LOCKED=<bool>`     | `true` si le device est verrouillé        |

**Algorithmes de hash supportés :** `SHA1`, `SHA256`, `SHA384`, `SHA512`

### 5.3 AUTH_INIT — Échange de nonces

Voir **DT-AURORA-AUTH-001 §2**.

### 5.4 AUTH — Authentification

Voir **DT-AURORA-AUTH-001 §3**.

### 5.5 CHPASSWD — Changement de mot de passe

Voir **DT-AURORA-AUTH-001 §4**.

### 5.6 PING

Test de connectivité simple.

```
→ LINK\x1fAURORA\x1fPING\0
← LINK\x1fAURORA\x1fRETURN\x1fPING\x1fPONG\0
```

### 5.7 UPLOAD — Téléversement de programme

Voir **DT-AURORA-UPLOAD-001**.

### 5.8 DONE

Signale la fin d'un échange.

```
→ LINK\x1fAURORA\x1fDONE\0
← LINK\x1fAURORA\x1fRETURN\x1fDONE\x1fOK\0
```

### 5.9 GETINPUT — Notification d'entrées

Voir **DT-AURORA-IO-001**.

### 5.10 Commande inconnue

Pour toute commande non reconnue, le device répond :

```
← LINK\x1fAURORA\x1fRETURN\x1f<COMMAND>\x1fERR\x1fUNKNOWN_COMMAND\0
```

---

## 6. Diagramme de séquence typique

```
PC (Aurora-LINK)                          Device (STM32)
      │                                        │
      │──── GETAPP ───────────────────────────→│
      │←─── RETURN GETAPP AURORA ──────────────│
      │                                        │
      │──── GETV ─────────────────────────────→│
      │←─── RETURN GETV LINKv2.0 ... ─────────│
      │                                        │
      │──── AUTH_INIT <clientNonce> ──────────→│
      │←─── RETURN AUTH_INIT <deviceNonce> ────│
      │                                        │
      │──── AUTH <digest> ────────────────────→│
      │←─── RETURN AUTH OK ────────────────────│
      │                                        │
      │←─── GETINPUT 0000000000 ───────────────│  (push initial)
      │                                        │
      │──── UPLOAD START <size> ──────────────→│
      │←─── RETURN UPLOAD OK ──────────────────│
      │──── UPLOAD DATA 0 <hex> ──────────────→│
      │←─── RETURN UPLOAD OK ──────────────────│
      │──── UPLOAD DATA 1 <hex> ──────────────→│
      │←─── RETURN UPLOAD OK ──────────────────│
      │──── UPLOAD END ───────────────────────→│
      │←─── RETURN UPLOAD OK ──────────────────│
      │                                        │
      │──── DONE ─────────────────────────────→│
      │←─── RETURN DONE OK ────────────────────│
      │                                        │
      │←─── GETINPUT 0100000000 ───────────────│  (push sur changement)
```

---

## 7. Timeout

Le logiciel PC utilise un **timeout de 2 secondes** par commande. Le firmware doit répondre dans ce délai.

---

## 8. Implémentation STM32 — Notes

### 8.1 Réception

1. Accumuler les octets reçus via USB CDC dans un buffer circulaire.
2. Chercher le délimiteur `\0`.
3. Extraire la trame complète et la parser en splitant sur `\x1F`.
4. Vérifier que `parts[0] == "LINK"`.
5. Dispatcher selon `parts[1]` (APP-ID) et `parts[2]` (commande).

### 8.2 Émission

1. Construire la trame : `"LINK\x1f" + APP_ID + "\x1f" + COMMAND + "\x1f" + ARGS... + "\0"`.
2. Si la trame dépasse 64 octets, la découper en chunks de 64 octets et les émettre séquentiellement via `CDC_Transmit_FS()`.

### 8.3 Structure C suggérée

```c
#define LINK_SEPARATOR      '\x1F'
#define LINK_TERMINATOR     '\0'
#define LINK_MAX_FRAME_SIZE 512
#define LINK_MAX_ARGS       16
#define LINK_APP_ID         "AURORA"

typedef struct {
    char*    app_id;
    char*    command;
    char*    args[LINK_MAX_ARGS];
    uint8_t  arg_count;
} LinkFrame;

typedef enum {
    LINK_CMD_GETAPP,
    LINK_CMD_GETV,
    LINK_CMD_AUTH_INIT,
    LINK_CMD_AUTH,
    LINK_CMD_CHPASSWD,
    LINK_CMD_PING,
    LINK_CMD_UPLOAD,
    LINK_CMD_DONE,
    LINK_CMD_UNKNOWN,
} LinkCommandType;
```

---

## 9. Documents liés

| Référence           | Titre                                    |
|----------------------|------------------------------------------|
| DT-AURORA-AUTH-001   | Authentification et sécurité             |
| DT-AURORA-MEM-001    | Format mémoire .flora                    |
| DT-AURORA-UPLOAD-001 | Protocole de téléversement               |
| DT-AURORA-IO-001     | Entrées/Sorties et GETINPUT              |

---

© 2025 — Projet Aurora-LINK
