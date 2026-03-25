# DT-AURORA-AUTH-001 — Authentification et sécurité

**Révision :** A  
**Date :** 2025-07  
**Portée :** Firmware STM32 + Application PC Aurora-LINK  

---

## 1. Présentation

Le module Aurora supporte un mécanisme d'authentification par **challenge-response** basé sur des nonces aléatoires et un algorithme de hash négocié. Le mot de passe n'est **jamais transmis en clair**. Le device ne stocke que `HASH(password)`, jamais le mot de passe en clair.

### 1.1 Principes de sécurité

- **Pas de mot de passe en clair** sur le lien série — ni en transmission, ni en stockage.
- **Nonces aléatoires** — empêchent les attaques par rejeu.
- **Algorithme négocié** — le device annonce son algorithme via `GETV`, le client le vérifie.
- **Double hash** — le device stocke `HASH(password)` et compare `HASH(nonces + HASH(password))`.

### 1.2 Algorithmes supportés

| Valeur GETV | Algorithme  | Taille digest |
|-------------|-------------|---------------|
| `SHA1`      | SHA-1       | 20 bytes (40 hex) |
| `SHA256`    | SHA-256     | 32 bytes (64 hex) |
| `SHA384`    | SHA-384     | 48 bytes (96 hex) |
| `SHA512`    | SHA-512     | 64 bytes (128 hex) |

L'algorithme par défaut est **SHA-256**.

---

## 2. AUTH_INIT — Échange de nonces

### 2.1 Objectif

Établir une paire de nonces aléatoires (client + device) qui seront utilisés pour le calcul du challenge-response.

### 2.2 Protocole

```
→ LINK\x1fAURORA\x1fAUTH_INIT\x1f<clientNonce>\0
← LINK\x1fAURORA\x1fRETURN\x1fAUTH_INIT\x1f<deviceNonce>\0
```

### 2.3 Paramètres

| Champ         | Taille          | Description                           |
|---------------|-----------------|---------------------------------------|
| `clientNonce` | 64 hex chars    | Nombre aléatoire 256 bits, hex lowercase |
| `deviceNonce` | 64 hex chars    | Nombre aléatoire 256 bits, hex uppercase |

### 2.4 Génération des nonces

- **Client (PC) :** `RandomNumberGenerator.GetBytes(32)` → hex lowercase (64 caractères).
- **Device (STM32) :** `HAL_RNG_GenerateRandomNumber()` × 8 → hex uppercase (64 caractères). Alternativement, utiliser le RNG hardware si disponible, ou un PRNG cryptographique.

### 2.5 Stockage temporaire

Le device doit conserver `clientNonce` et `deviceNonce` en mémoire RAM pour la durée de la session. Ils sont utilisés par `AUTH` et `CHPASSWD`.

### 2.6 Erreurs

| Réponse                        | Cause                     |
|--------------------------------|---------------------------|
| `RETURN AUTH_INIT ERR MISSING_NONCE` | Nonce client absent |

### 2.7 Implémentation STM32

```c
// Stockage temporaire en RAM
static char client_nonce[65];  // 64 hex + \0
static char device_nonce[65];

void handle_auth_init(LinkFrame* frame) {
    if (frame->arg_count < 1 || strlen(frame->args[0]) == 0) {
        link_send_return("AUTH_INIT", "ERR", "MISSING_NONCE", NULL);
        return;
    }
    
    // Sauvegarder le nonce client
    strncpy(client_nonce, frame->args[0], 64);
    client_nonce[64] = '\0';
    
    // Générer le nonce device (256 bits = 8 × uint32)
    uint32_t rng_val;
    for (int i = 0; i < 8; i++) {
        HAL_RNG_GenerateRandomNumber(&hrng, &rng_val);
        sprintf(&device_nonce[i * 8], "%08X", rng_val);
    }
    device_nonce[64] = '\0';
    
    link_send_return("AUTH_INIT", device_nonce, NULL);
}
```

---

## 3. AUTH — Authentification challenge-response

### 3.1 Objectif

Vérifier que le client connaît le mot de passe sans le transmettre.

### 3.2 Protocole

```
→ LINK\x1fAURORA\x1fAUTH\x1f<digest>\0
← LINK\x1fAURORA\x1fRETURN\x1fAUTH\x1fOK\0       (succès)
← LINK\x1fAURORA\x1fRETURN\x1fAUTH\x1fERR\0      (échec)
```

### 3.3 Calcul du digest (côté client)

```
passwordHash = HASH(password)                          // ex: SHA-256 du mot de passe en UTF-8
digest       = HASH(clientNonce + deviceNonce + passwordHash)  // concaténation des chaînes hex
```

Le résultat `digest` est envoyé en **hex uppercase**.

### 3.4 Vérification (côté device)

Le device stocke `storedHash = HASH(password)` en Flash. Il calcule :

```
expected = HASH(clientNonce + deviceNonce + storedHash)
```

Si `digest == expected` (comparaison case-insensitive), le device déverrouille et répond `OK`.

### 3.5 Après authentification réussie

1. Le device passe en état **déverrouillé** (`locked = false`).
2. Le device passe en état **connecté** (`connected = true`).
3. Le device envoie immédiatement une trame `GETINPUT` avec l'état courant des entrées.

### 3.6 Erreurs

| Réponse                       | Cause                              |
|-------------------------------|-------------------------------------|
| `RETURN AUTH ERR`             | Hash incorrect (mot de passe faux)  |
| `RETURN AUTH ERR NO_AUTH_INIT`| AUTH_INIT non effectué au préalable |

### 3.7 Diagramme

```
Client                                   Device
  │                                         │
  │  clientNonce = random(256 bits)         │
  │                                         │
  │── AUTH_INIT <clientNonce> ─────────────→│
  │                                         │  deviceNonce = random(256 bits)
  │                                         │  stocker clientNonce, deviceNonce
  │←── RETURN AUTH_INIT <deviceNonce> ──────│
  │                                         │
  │  passwordHash = HASH(password)          │
  │  digest = HASH(cN + dN + passwordHash)  │
  │                                         │
  │── AUTH <digest> ───────────────────────→│
  │                                         │  expected = HASH(cN + dN + storedHash)
  │                                         │  if digest == expected → OK
  │←── RETURN AUTH OK ─────────────────────│
  │                                         │  → déverrouiller
  │←── GETINPUT 0000000000 ────────────────│  → push état entrées
```

### 3.8 Implémentation STM32

```c
// storedHash est lu depuis la Flash (HASH(password) stocké à la programmation)
extern char stored_password_hash[129];  // max SHA-512 = 128 hex + \0

void handle_auth(LinkFrame* frame) {
    if (strlen(client_nonce) == 0 || strlen(device_nonce) == 0) {
        link_send_return("AUTH", "ERR", "NO_AUTH_INIT", NULL);
        return;
    }
    
    if (frame->arg_count < 1) {
        link_send_return("AUTH", "ERR", NULL);
        return;
    }
    
    // Calculer HASH(clientNonce + deviceNonce + storedHash)
    char concat[512];
    snprintf(concat, sizeof(concat), "%s%s%s", 
             client_nonce, device_nonce, stored_password_hash);
    
    char expected[129];
    compute_hash(hash_method, concat, strlen(concat), expected);
    
    // Comparaison case-insensitive
    if (strcasecmp(frame->args[0], expected) == 0) {
        device_locked = false;
        device_connected = true;
        link_send_return("AUTH", "OK", NULL);
        push_input_state();  // Envoyer GETINPUT immédiatement
    } else {
        link_send_return("AUTH", "ERR", NULL);
    }
}
```

---

## 4. CHPASSWD — Changement de mot de passe

### 4.1 Prérequis

- Le device doit être **déverrouillé** (authentifié via AUTH).
- Un échange `AUTH_INIT` doit avoir eu lieu (nonces valides en mémoire).

### 4.2 Protocole

```
→ LINK\x1fAURORA\x1fCHPASSWD\x1f<hashedOld>\x1f<encryptedNewHash>\0
← LINK\x1fAURORA\x1fRETURN\x1fCHPASSWD\x1fOK\0
```

### 4.3 Paramètres

| Champ               | Description                                                    |
|----------------------|----------------------------------------------------------------|
| `hashedOld`          | `HASH(clientNonce + deviceNonce + HASH(ancienMotDePasse))`     |
| `encryptedNewHash`   | `HEX(XOR(HASH(nouveauMotDePasse)_bytes, clé_chiffrement))`    |

### 4.4 Clé de chiffrement

```
clé_chiffrement = HASH(deviceNonce + clientNonce + HASH(ancienMotDePasse))
```

> **Important :** L'ordre des nonces est **inversé** par rapport au hash de vérification (`hashedOld`). Cela garantit que la clé de chiffrement est toujours différente du hash de vérification envoyé sur le lien.

### 4.5 Déchiffrement côté device

```
newPasswordHash_bytes = XOR(encryptedNewHash_bytes, clé_chiffrement_bytes)
newPasswordHash       = HEX(newPasswordHash_bytes)  // → stocker en Flash
```

La clé est appliquée cycliquement si le hash est plus long que la clé (`key[i % key_length]`).

### 4.6 Vérification côté device

1. Vérifier que le device n'est **pas verrouillé** → sinon `ERR LOCKED`.
2. Vérifier que `AUTH_INIT` a été effectué → sinon `ERR NO_AUTH_INIT`.
3. Calculer `expected = HASH(clientNonce + deviceNonce + storedHash)`.
4. Comparer `hashedOld` avec `expected` → sinon `ERR INVALID_PASSWORD`.
5. Calculer la clé : `encKey = HASH(deviceNonce + clientNonce + storedHash)`.
6. Déchiffrer : `newHash = XOR(encryptedNewHash, encKey)`.
7. Vérifier que `newHash` n'est pas vide → sinon `ERR WEAK_PASSWORD`.
8. Stocker `newHash` en Flash comme nouveau `storedPasswordHash`.
9. Répondre `OK`.

### 4.7 Codes d'erreur

| Réponse                            | Cause                                    |
|-------------------------------------|------------------------------------------|
| `RETURN CHPASSWD OK`               | Mot de passe modifié avec succès         |
| `RETURN CHPASSWD ERR LOCKED`       | Device verrouillé (non authentifié)      |
| `RETURN CHPASSWD ERR NO_AUTH_INIT` | Aucun échange de nonces en cours         |
| `RETURN CHPASSWD ERR INVALID_PASSWORD` | Ancien mot de passe incorrect        |
| `RETURN CHPASSWD ERR WEAK_PASSWORD`| Nouveau hash vide                        |
| `RETURN CHPASSWD ERR MISSING_ARGS` | Arguments manquants                      |
| `RETURN CHPASSWD ERR INVALID_HEX`  | Format hex invalide                      |

### 4.8 Diagramme

```
Client                                        Device
  │                                              │
  │  (AUTH_INIT déjà effectué, device déverrouillé)
  │                                              │
  │  oldHash  = HASH(ancienMotDePasse)           │
  │  hashedOld = HASH(cN + dN + oldHash)         │
  │  encKey   = HASH(dN + cN + oldHash)          │  ← nonces inversés
  │  newHash  = HASH(nouveauMotDePasse)           │
  │  encrypted = XOR(newHash_bytes, encKey_bytes) │
  │                                              │
  │── CHPASSWD <hashedOld> <encrypted_hex> ────→│
  │                                              │  1. Vérifier non-verrouillé
  │                                              │  2. expected = HASH(cN + dN + storedHash)
  │                                              │  3. Comparer hashedOld == expected
  │                                              │  4. encKey = HASH(dN + cN + storedHash)
  │                                              │  5. newHash = XOR(encrypted, encKey)
  │                                              │  6. Stocker newHash en Flash
  │←── RETURN CHPASSWD OK ─────────────────────│
```

### 4.9 Implémentation STM32

```c
void handle_chpasswd(LinkFrame* frame) {
    if (device_locked) {
        link_send_return("CHPASSWD", "ERR", "LOCKED", NULL);
        return;
    }
    if (strlen(client_nonce) == 0 || strlen(device_nonce) == 0) {
        link_send_return("CHPASSWD", "ERR", "NO_AUTH_INIT", NULL);
        return;
    }
    if (frame->arg_count < 2) {
        link_send_return("CHPASSWD", "ERR", "MISSING_ARGS", NULL);
        return;
    }
    
    // 1. Vérifier l'ancien mot de passe
    char concat_verify[512];
    snprintf(concat_verify, sizeof(concat_verify), "%s%s%s",
             client_nonce, device_nonce, stored_password_hash);
    
    char expected[129];
    compute_hash(hash_method, concat_verify, strlen(concat_verify), expected);
    
    if (strcasecmp(frame->args[0], expected) != 0) {
        link_send_return("CHPASSWD", "ERR", "INVALID_PASSWORD", NULL);
        return;
    }
    
    // 2. Calculer la clé de chiffrement (nonces inversés)
    char concat_key[512];
    snprintf(concat_key, sizeof(concat_key), "%s%s%s",
             device_nonce, client_nonce, stored_password_hash);
    
    char enc_key_hex[129];
    compute_hash(hash_method, concat_key, strlen(concat_key), enc_key_hex);
    
    uint8_t enc_key[64];
    size_t key_len = hex_to_bytes(enc_key_hex, enc_key, sizeof(enc_key));
    
    // 3. Déchiffrer le nouveau hash par XOR
    uint8_t encrypted_bytes[64];
    size_t enc_len = hex_to_bytes(frame->args[1], encrypted_bytes, sizeof(encrypted_bytes));
    
    uint8_t new_hash_bytes[64];
    for (size_t i = 0; i < enc_len; i++) {
        new_hash_bytes[i] = encrypted_bytes[i] ^ enc_key[i % key_len];
    }
    
    // 4. Convertir en hex et stocker en Flash
    bytes_to_hex(new_hash_bytes, enc_len, stored_password_hash);
    flash_write_password_hash(stored_password_hash);
    
    link_send_return("CHPASSWD", "OK", NULL);
}
```

---

## 5. Stockage du mot de passe en Flash

Le device ne stocke **jamais** le mot de passe en clair. Seul `HASH(password)` est écrit en Flash.

### 5.1 Zone de stockage

| Adresse         | Taille      | Contenu                          |
|-----------------|-------------|----------------------------------|
| Flash page dédiée | 128 bytes | `HASH(password)` en hex ASCII   |

### 5.2 Écriture

Lors d'un changement de mot de passe (`CHPASSWD`), le firmware doit :

1. Effacer la page Flash dédiée.
2. Écrire le nouveau `HASH(password)` en hex ASCII.
3. Vérifier l'écriture par relecture.

### 5.3 Valeur par défaut

À la première programmation du firmware, `HASH("aurora")` doit être pré-calculé et stocké en Flash. Le mot de passe par défaut est `aurora`.

```c
// Valeur SHA-256 de "aurora" (pré-calculée)
// sha256("aurora") = "41d1f64e2e3..."
#define DEFAULT_PASSWORD_HASH "41d1f64e2e3..."
```

---

## 6. Documents liés

| Référence           | Titre                                    |
|----------------------|------------------------------------------|
| DT-AURORA-PROTO-001  | Protocole de communication AURORA        |
| DT-AURORA-MEM-001    | Format mémoire .flora                    |
| DT-AURORA-UPLOAD-001 | Protocole de téléversement               |
| DT-AURORA-IO-001     | Entrées/Sorties et GETINPUT              |

---

© 2025 — Projet Aurora-LINK
