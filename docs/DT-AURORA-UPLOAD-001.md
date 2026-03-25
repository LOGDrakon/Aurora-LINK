# DT-AURORA-UPLOAD-001 — Protocole de téléversement

**Révision :** A  
**Date :** 2025-07  
**Portée :** Firmware STM32 + Application PC Aurora-LINK  

---

## 1. Introduction

Ce document décrit le protocole de transfert d'un programme `.flora` depuis le logiciel PC vers le module Aurora via la commande `UPLOAD`. Le protocole est conçu pour être fiable sur une liaison série USB avec accusé de réception par paquet.

---

## 2. Vue d'ensemble

Le téléversement se déroule en **3 phases** :

```
Phase 1 : START  — Annonce de la taille totale
Phase 2 : DATA   — Envoi séquentiel des paquets de données
Phase 3 : END    — Vérification d'intégrité par le device
```

Chaque phase attend un accusé de réception (`OK` ou `ERR`) avant de passer à la suivante.

---

## 3. Phase 1 : UPLOAD START

### 3.1 Commande

```
→ LINK\x1fAURORA\x1fUPLOAD\x1fSTART\x1f<taille>\0
← LINK\x1fAURORA\x1fRETURN\x1fUPLOAD\x1fOK\0
```

### 3.2 Paramètres

| Champ    | Type   | Description                              |
|----------|--------|------------------------------------------|
| `taille` | string | Nombre total d'octets à transmettre (décimal) |

### 3.3 Comportement device

1. Initialiser le buffer de réception (effacer les données précédentes).
2. Enregistrer la taille attendue.
3. Réinitialiser le compteur de séquence à 0.
4. Répondre `OK`.

### 3.4 Erreurs possibles

| Réponse                         | Cause                    |
|----------------------------------|--------------------------|
| `RETURN UPLOAD ERR MISSING_SIZE` | Taille non fournie       |
| `RETURN UPLOAD ERR INVALID_SIZE` | Taille non numérique     |

### 3.5 Implémentation STM32

```c
static uint8_t  upload_buffer[2048];  // taille max d'un .flora
static uint32_t upload_expected_size = 0;
static uint32_t upload_received = 0;
static uint32_t upload_seq = 0;

void handle_upload_start(const char* size_str) {
    upload_expected_size = atoi(size_str);
    upload_received = 0;
    upload_seq = 0;
    memset(upload_buffer, 0xFF, sizeof(upload_buffer));
    
    link_send_return("UPLOAD", "OK", NULL);
}
```

---

## 4. Phase 2 : UPLOAD DATA

### 4.1 Commande

```
→ LINK\x1fAURORA\x1fUPLOAD\x1fDATA\x1f<seq>\x1f<hex>\0
← LINK\x1fAURORA\x1fRETURN\x1fUPLOAD\x1fOK\0
```

### 4.2 Paramètres

| Champ | Type   | Description                                          |
|-------|--------|------------------------------------------------------|
| `seq` | string | Numéro de séquence (décimal, commence à 0)          |
| `hex` | string | Données binaires encodées en hexadécimal (uppercase) |

### 4.3 Taille des paquets

Le logiciel PC envoie des paquets de **128 octets bruts maximum** (256 caractères hex). Ce choix offre un bon compromis entre vitesse et fiabilité.

> **Note :** Les trames LINK dépassant 64 octets sont automatiquement fragmentées par la couche transport. Le firmware reçoit la trame complète après réassemblage.

### 4.4 Numéro de séquence

- Commence à `0`.
- Incrémenté de `1` à chaque paquet.
- Le device vérifie que le numéro reçu correspond au numéro attendu.
- En cas de mismatch → `ERR SEQ_MISMATCH`.

### 4.5 Traitement côté device

1. Vérifier le numéro de séquence.
2. Décoder les données hexadécimales en binaire.
3. Copier les données dans le buffer à la position courante.
4. Mettre à jour le compteur `upload_received`.
5. Incrémenter `upload_seq`.
6. Répondre `OK`.

### 4.6 Erreurs possibles

| Réponse                          | Cause                           |
|-----------------------------------|---------------------------------|
| `RETURN UPLOAD ERR MISSING_DATA`  | Arguments manquants             |
| `RETURN UPLOAD ERR INVALID_SEQ`   | Numéro de séquence non numérique|
| `RETURN UPLOAD ERR SEQ_MISMATCH`  | Séquence inattendue             |
| `RETURN UPLOAD ERR INVALID_HEX`   | Données hex invalides           |

### 4.7 Implémentation STM32

```c
void handle_upload_data(uint32_t seq, const char* hex_data) {
    if (seq != upload_seq) {
        link_send_return("UPLOAD", "ERR", "SEQ_MISMATCH", NULL);
        return;
    }
    
    // Décoder le hex en binaire
    size_t hex_len = strlen(hex_data);
    size_t byte_len = hex_len / 2;
    
    if (upload_received + byte_len > sizeof(upload_buffer)) {
        link_send_return("UPLOAD", "ERR", "OVERFLOW", NULL);
        return;
    }
    
    if (!hex_to_bytes(hex_data, &upload_buffer[upload_received], byte_len)) {
        link_send_return("UPLOAD", "ERR", "INVALID_HEX", NULL);
        return;
    }
    
    upload_received += byte_len;
    upload_seq++;
    
    link_send_return("UPLOAD", "OK", NULL);
}
```

---

## 5. Phase 3 : UPLOAD END

### 5.1 Commande

```
→ LINK\x1fAURORA\x1fUPLOAD\x1fEND\0
← LINK\x1fAURORA\x1fRETURN\x1fUPLOAD\x1fOK\0       (succès)
← LINK\x1fAURORA\x1fRETURN\x1fUPLOAD\x1fERR\x1f<raison>\0  (échec)
```

### 5.2 Vérifications d'intégrité

Le device effectue 3 vérifications dans l'ordre :

1. **Taille** — `upload_received == upload_expected_size`
2. **Signature FLOR** — les 4 derniers octets sont `0x464C4F52` (little-endian → bytes `52 4F 4C 46`)
3. **CRC-32** — CRC IEEE 802.3 calculé sur tout sauf les 8 derniers octets (CRC + signature), comparé au CRC stocké aux octets `[-8..-5]`

### 5.3 Erreurs possibles

| Réponse                          | Cause                              |
|-----------------------------------|------------------------------------|
| `RETURN UPLOAD ERR SIZE_MISMATCH` | Taille reçue ≠ taille attendue    |
| `RETURN UPLOAD ERR INTEGRITY`     | Signature ou CRC-32 incorrect     |

### 5.4 Après succès

1. Répondre `OK`.
2. Écrire le contenu du buffer en Flash (page de configuration).
3. Relire la Flash et vérifier l'écriture (optionnel mais recommandé).
4. Réinitialiser l'état d'upload.

### 5.5 Réinitialisation

Dans tous les cas (succès ou échec), l'état d'upload est réinitialisé après `END` :

```c
upload_buffer → effacé
upload_expected_size = 0
upload_seq = 0
upload_received = 0
```

### 5.6 Implémentation STM32

```c
void handle_upload_end(void) {
    // 1. Vérifier la taille
    if (upload_received != upload_expected_size) {
        link_send_return("UPLOAD", "ERR", "SIZE_MISMATCH", NULL);
        goto cleanup;
    }
    
    // 2. Vérifier la signature FLOR (4 derniers octets)
    uint32_t signature;
    memcpy(&signature, &upload_buffer[upload_received - 4], 4);
    if (signature != 0x464C4F52) {
        link_send_return("UPLOAD", "ERR", "INTEGRITY", NULL);
        goto cleanup;
    }
    
    // 3. Vérifier le CRC-32
    //    CRC couvre tout sauf les 8 derniers octets (CRC + signature)
    uint32_t stored_crc;
    memcpy(&stored_crc, &upload_buffer[upload_received - 8], 4);
    uint32_t computed_crc = compute_crc32(upload_buffer, upload_received - 8);
    
    if (stored_crc != computed_crc) {
        link_send_return("UPLOAD", "ERR", "INTEGRITY", NULL);
        goto cleanup;
    }
    
    // 4. Écrire en Flash
    if (!flash_write_config(upload_buffer, upload_received)) {
        link_send_return("UPLOAD", "ERR", "FLASH_WRITE", NULL);
        goto cleanup;
    }
    
    link_send_return("UPLOAD", "OK", NULL);
    
cleanup:
    memset(upload_buffer, 0xFF, sizeof(upload_buffer));
    upload_expected_size = 0;
    upload_received = 0;
    upload_seq = 0;
}
```

---

## 6. Diagramme de séquence complet

```
PC (Aurora-LINK)                           Device (STM32)
      │                                          │
      │  Sérialiser config → .flora (N bytes)    │
      │  Vérifier CRC en local                   │
      │                                          │
      │── UPLOAD START <N> ──────────────────→│
      │                                          │  buffer = alloc(N)
      │                                          │  seq = 0
      │←── RETURN UPLOAD OK ─────────────────│
      │                                          │
      │── UPLOAD DATA 0 <hex_chunk_0> ───────→│
      │                                          │  copier dans buffer
      │                                          │  seq++
      │←── RETURN UPLOAD OK ─────────────────│
      │                                          │
      │── UPLOAD DATA 1 <hex_chunk_1> ───────→│
      │←── RETURN UPLOAD OK ─────────────────│
      │                                          │
      │── ...                                    │
      │                                          │
      │── UPLOAD DATA n <hex_chunk_n> ───────→│
      │←── RETURN UPLOAD OK ─────────────────│
      │                                          │
      │── UPLOAD END ────────────────────────→│
      │                                          │  1. taille OK ?
      │                                          │  2. signature FLOR OK ?
      │                                          │  3. CRC-32 OK ?
      │                                          │  4. écrire en Flash
      │←── RETURN UPLOAD OK ─────────────────│
```

---

## 7. Calcul du nombre de paquets

```
MaxChunkSize = 128 bytes bruts (256 chars hex)
num_packets  = ceil(file_size / MaxChunkSize)
```

**Exemples :**

| Taille .flora | Paquets DATA |
|---------------|-------------|
| 48 bytes      | 1           |
| 128 bytes     | 1           |
| 129 bytes     | 2           |
| 340 bytes     | 3           |
| 2048 bytes    | 16          |

---

## 8. Gestion des erreurs et reprise

En cas d'erreur sur un paquet `DATA` :

- Le logiciel PC lève une exception et **arrête** le téléversement.
- Il n'y a **pas de mécanisme de retry** automatique au niveau applicatif.
- L'utilisateur doit relancer le téléversement complet.

En cas de timeout (le device ne répond pas dans les 2 secondes) :

- Le logiciel PC lève une `TimeoutException`.
- Le téléversement est interrompu.

---

## 9. Commande inconnue UPLOAD

Pour toute sous-commande UPLOAD non reconnue :

```
← LINK\x1fAURORA\x1fRETURN\x1fUPLOAD\x1fERR\x1fUNKNOWN_SUBCOMMAND\0
```

---

## 10. Documents liés

| Référence           | Titre                                    |
|----------------------|------------------------------------------|
| DT-AURORA-PROTO-001  | Protocole de communication AURORA        |
| DT-AURORA-AUTH-001   | Authentification et sécurité             |
| DT-AURORA-MEM-001    | Format mémoire .flora                    |
| DT-AURORA-IO-001     | Entrées/Sorties et GETINPUT              |

---

© 2025 — Projet Aurora-LINK
