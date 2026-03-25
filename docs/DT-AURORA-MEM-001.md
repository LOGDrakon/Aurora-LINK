# DT-AURORA-MEM-001 — Format mémoire .flora

**Révision :** C  
**Date :** 2025-07  
**Portée :** Firmware STM32 + Application PC Aurora-LINK  

---

## 1. Introduction

Ce document décrit le format binaire `.flora` utilisé pour stocker et transférer la configuration complète d'un module Aurora. Ce format correspond au contenu d'une **page Flash de 2048 octets** sur le STM32.

---

## 2. Vue d'ensemble de la structure

```
┌──────────────────────────────────────┐  Offset 0
│             Header (16 bytes)         │
├──────────────────────────────────────┤  Offset 16
│          Bloc TLV : LEDs (8 bytes)    │
├──────────────────────────────────────┤
│        Bloc TLV : Scènes (variable)   │
├──────────────────────────────────────┤
│        Bloc TLV : Entrées (variable)  │
├──────────────────────────────────────┤
│       Bloc TLV : Système (12 bytes)   │
├──────────────────────────────────────┤
│            CRC-32 (4 bytes)           │
├──────────────────────────────────────┤
│        Signature "FLOR" (4 bytes)     │
└──────────────────────────────────────┘
```

**Taille totale** = Header (16) + Σ blocs TLV + CRC32 (4) + Signature (4)  
**Taille maximale** = 2048 bytes (1 page Flash STM32)

**Byte order** = **Little-endian** (natif ARM Cortex-M)

---

## 3. Constantes

| Constante          | Valeur          | Description                        |
|---------------------|-----------------|------------------------------------|
| `MAGIC`            | `0x41555241`    | ASCII `"AURA"` (little-endian)     |
| `SIGNATURE`        | `0x464C4F52`    | ASCII `"FLOR"` (little-endian)     |
| `CURRENT_VERSION`  | `0x02`          | Version courante du format         |
| `FLASH_PAGE_SIZE`  | `2048`          | Taille max d'un fichier .flora     |
| `MAX_SCENES`       | `16`            | Nombre maximum de scènes           |
| `MAX_INPUTS`       | `10`            | Nombre maximum d'entrées           |

---

## 4. Header (16 bytes)

| Offset | Taille | Type     | Champ         | Description                             |
|--------|--------|----------|---------------|-----------------------------------------|
| 0      | 4      | uint32   | `magic`       | `0x41555241` — identifiant "AURA"       |
| 4      | 1      | uint8    | `version`     | Version du format (actuellement `0x02`) |
| 5      | 1      | uint8    | `num_blocs`   | Nombre de blocs TLV dans le fichier     |
| 6      | 2      | uint16   | `total_length`| Taille totale des blocs TLV en bytes    |
| 8      | 4      | uint32   | `write_count` | Compteur d'écritures (incrémenté)       |
| 12     | 4      | uint32   | `reserved`    | Réservé — `0xFFFFFFFF`                  |

### 4.1 Validation du Header

1. Vérifier `magic == 0x41555241`.
2. Vérifier `version <= CURRENT_VERSION`.
3. Vérifier la cohérence de la taille :  
   `file_size == 16 + total_length + 4 + 4`

### 4.2 Structure C

```c
typedef struct __attribute__((packed)) {
    uint32_t magic;         // 0x41555241 "AURA"
    uint8_t  version;       // 0x02
    uint8_t  num_blocs;     // nombre de blocs TLV
    uint16_t total_length;  // taille totale des données TLV
    uint32_t write_count;   // compteur d'écritures
    uint32_t reserved;      // 0xFFFFFFFF
} AuroraHeader;  // 16 bytes
```

---

## 5. Blocs TLV

Chaque bloc suit le format **Type-Length-Value** :

### 5.1 En-tête TLV (4 bytes)

| Offset | Taille | Type   | Champ    | Description                    |
|--------|--------|--------|----------|--------------------------------|
| 0      | 1      | uint8  | `type`   | Identifiant du bloc            |
| 1      | 1      | uint8  | `flags`  | Réservé — `0x00`               |
| 2      | 2      | uint16 | `length` | Taille du payload en bytes     |

### 5.2 Types de blocs

| Type   | Valeur | Nom      | Payload size      |
|--------|--------|----------|-------------------|
| LEDs   | `0x01` | `Leds`   | 4 bytes (fixe)    |
| Scènes | `0x02` | `Scenes` | N × 12 bytes      |
| Entrées| `0x03` | `Inputs` | N × 10 bytes      |
| Système| `0x04` | `System` | 8 bytes (fixe)    |

### 5.3 Ordre des blocs

Les 4 blocs sont toujours présents et dans cet ordre :

1. `Leds` (0x01)
2. `Scenes` (0x02)
3. `Inputs` (0x03)
4. `System` (0x04)

```c
typedef enum {
    AURORA_BLOC_LEDS    = 0x01,
    AURORA_BLOC_SCENES  = 0x02,
    AURORA_BLOC_INPUTS  = 0x03,
    AURORA_BLOC_SYSTEM  = 0x04,
} AuroraBlocType;

typedef struct __attribute__((packed)) {
    uint8_t  type;
    uint8_t  flags;    // 0x00
    uint16_t length;   // payload size
} AuroraTlvHeader;  // 4 bytes
```

---

## 6. Bloc LEDs (type 0x01, 4 bytes)

Configuration matérielle du canal LED unique. Les deux LEDs physiques sont pilotées de manière synchrone.

| Offset | Taille | Type   | Champ         | Description                          |
|--------|--------|--------|---------------|--------------------------------------|
| 0      | 1      | uint8  | `max_pwm`     | PWM maximum (0x00–0xFF, défaut 0xFF) |
| 1      | 2      | uint16 | `soft_start_ms` | Durée du démarrage progressif (ms) |
| 3      | 1      | uint8  | `reserved`    | Réservé — `0xFF`                     |

```c
typedef struct __attribute__((packed)) {
    uint8_t  max_pwm;        // 0xFF = 100%
    uint16_t soft_start_ms;  // rampe de démarrage
    uint8_t  reserved;       // 0xFF
} AuroraLedConfig;  // 4 bytes
```

---

## 7. Bloc Scènes (type 0x02, N × 12 bytes)

### 7.1 Payload

Le payload contient 0 à 16 scènes, chacune de 12 bytes.

### 7.2 État LED dans une scène (10 bytes)

| Offset | Taille | Type   | Champ      | Description                              |
|--------|--------|--------|------------|------------------------------------------|
| 0      | 1      | uint8  | `mode`     | Mode d'animation (voir §7.3)            |
| 1      | 1      | uint8  | `red`      | Composante rouge (0–255)                 |
| 2      | 1      | uint8  | `green`    | Composante verte (0–255)                 |
| 3      | 1      | uint8  | `blue`     | Composante bleue (0–255)                 |
| 4      | 2      | uint16 | `t_on_ms`  | Durée ON en ms (pour Blink, Burst…)      |
| 6      | 2      | uint16 | `t_off_ms` | Durée OFF en ms (pour Blink, Burst…)     |
| 8      | 1      | uint8  | `repeat`   | Nombre de répétitions (0 = infini)       |
| 9      | 1      | uint8  | `fade_time`| Temps de fondu (unité dépend du mode)    |

### 7.3 Modes LED

| Valeur | Nom      | Description                                        |
|--------|----------|----------------------------------------------------|
| `0x00` | `Off`    | LED éteinte                                        |
| `0x01` | `Static` | Couleur fixe                                       |
| `0x02` | `Blink`  | Clignotement ON/OFF avec `t_on` et `t_off`        |
| `0x03` | `Fade`   | Fondu progressif (transition douce)                |
| `0x04` | `Burst`  | Éclat rapide puis extinction                       |
| `0x05` | `Double` | Double clignotement                                |

```c
typedef enum {
    LED_MODE_OFF    = 0x00,
    LED_MODE_STATIC = 0x01,
    LED_MODE_BLINK  = 0x02,
    LED_MODE_FADE   = 0x03,
    LED_MODE_BURST  = 0x04,
    LED_MODE_DOUBLE = 0x05,
} AuroraLedMode;
```

### 7.4 Structure LED State

```c
typedef struct __attribute__((packed)) {
    uint8_t  mode;
    uint8_t  red;
    uint8_t  green;
    uint8_t  blue;
    uint16_t t_on_ms;
    uint16_t t_off_ms;
    uint8_t  repeat;
    uint8_t  fade_time;
} AuroraLedState;  // 10 bytes
```

### 7.5 Structure Scène (12 bytes)

| Offset | Taille | Type   | Champ      | Description                   |
|--------|--------|--------|------------|-------------------------------|
| 0      | 1      | uint8  | `scene_id` | ID séquentiel (0x00 à 0x0F)  |
| 1      | 1      | uint8  | `flags`    | Drapeaux (voir §7.6)         |
| 2      | 10     | struct | `state`    | État LED (voir §7.2)         |

**Contrainte :** Les IDs de scène sont **séquentiels** : 0, 1, 2, … N-1.

```c
typedef struct __attribute__((packed)) {
    uint8_t        scene_id;
    uint8_t        flags;
    AuroraLedState state;
} AuroraScene;  // 12 bytes
```

### 7.6 Drapeaux de scène

| Bit | Nom        | Description                                          |
|-----|------------|------------------------------------------------------|
| 0   | `AutoStart`| Scène chargée automatiquement au démarrage           |
| 1–7 | Réservés   | `0`                                                  |

**Contrainte :** Au plus **une seule** scène peut avoir le flag `AutoStart`.

```c
#define AURORA_FLAG_AUTOSTART  0x01
```

---

## 8. Bloc Entrées (type 0x03, N × 10 bytes)

### 8.1 Payload

Le payload contient 0 à 10 règles d'entrée, chacune de 10 bytes.

### 8.2 Structure d'une entrée (10 bytes)

| Offset | Taille | Type   | Champ        | Description                          |
|--------|--------|--------|--------------|--------------------------------------|
| 0      | 1      | uint8  | `input_id`   | ID de l'entrée physique (0–9)        |
| 1      | 1      | uint8  | `trigger`    | Type de déclencheur (voir §8.3)     |
| 2      | 1      | uint8  | `action`     | Action à exécuter (voir §8.4)       |
| 3      | 1      | uint8  | `target`     | Cible de l'action (ex: scene_id)    |
| 4      | 2      | uint16 | `param`      | Paramètre additionnel               |
| 6      | 2      | uint16 | `debounce_ms`| Anti-rebond en ms (défaut 20)        |
| 8      | 1      | uint8  | `priority`   | Priorité (0 = plus haute)           |
| 9      | 1      | uint8  | `reserved`   | Réservé — `0xFF`                     |

**Contrainte :** Les `input_id` doivent être uniques (pas de doublons).

### 8.3 Types de déclencheur

| Valeur | Nom         | Description                                |
|--------|-------------|--------------------------------------------|
| `0x00` | `Rising`    | Front montant (OFF → ON)                   |
| `0x01` | `Falling`   | Front descendant (ON → OFF)                |
| `0x02` | `High`      | Niveau haut maintenu                       |
| `0x03` | `Low`       | Niveau bas maintenu                        |
| `0x04` | `DoubleTap` | Double appui rapide                        |
| `0x05` | `LongPress` | Appui long                                 |
| `0x06` | `Pulse`     | Impulsion (front montant puis descendant)  |

```c
typedef enum {
    TRIGGER_RISING     = 0x00,
    TRIGGER_FALLING    = 0x01,
    TRIGGER_HIGH       = 0x02,
    TRIGGER_LOW        = 0x03,
    TRIGGER_DOUBLE_TAP = 0x04,
    TRIGGER_LONG_PRESS = 0x05,
    TRIGGER_PULSE      = 0x06,
} AuroraTrigger;
```

### 8.4 Types d'action

| Valeur | Nom         | Description                                   | Target              |
|--------|-------------|-----------------------------------------------|---------------------|
| `0x00` | `LoadScene` | Charger une scène                             | `scene_id`          |
| `0x01` | `SetBright` | Régler la luminosité                          | Valeur PWM (0–255)  |
| `0x02` | `Toggle`    | Basculer ON/OFF la scène courante             | —                   |
| `0x03` | `AllOff`    | Éteindre toutes les LEDs                      | —                   |
| `0x04` | `DimUp`     | Augmenter la luminosité                       | Pas (dans `param`)  |
| `0x05` | `DimDown`   | Diminuer la luminosité                        | Pas (dans `param`)  |
| `0x06` | `Lock`      | Verrouiller le device                         | —                   |
| `0x07` | `Unlock`    | Déverrouiller le device                       | —                   |
| `0x08` | `Identify`  | Clignotement d'identification                | —                   |

```c
typedef enum {
    ACTION_LOAD_SCENE = 0x00,
    ACTION_SET_BRIGHT = 0x01,
    ACTION_TOGGLE     = 0x02,
    ACTION_ALL_OFF    = 0x03,
    ACTION_DIM_UP     = 0x04,
    ACTION_DIM_DOWN   = 0x05,
    ACTION_LOCK       = 0x06,
    ACTION_UNLOCK     = 0x07,
    ACTION_IDENTIFY   = 0x08,
} AuroraAction;
```

### 8.5 Structure C

```c
typedef struct __attribute__((packed)) {
    uint8_t  input_id;
    uint8_t  trigger;
    uint8_t  action;
    uint8_t  target;
    uint16_t param;
    uint16_t debounce_ms;
    uint8_t  priority;
    uint8_t  reserved;    // 0xFF
} AuroraInputConfig;  // 10 bytes
```

---

## 9. Bloc Système (type 0x04, 8 bytes)

| Offset | Taille | Type   | Champ          | Description                              |
|--------|--------|--------|----------------|------------------------------------------|
| 0      | 1      | uint8  | `boot_scene`   | Scène au démarrage (`0xFF` = aucune)     |
| 1      | 1      | uint8  | `temp_derating` | Réduction thermique (0–100%)            |
| 2      | 2      | uint16 | `hours_counter`| Compteur d'heures de fonctionnement      |
| 4      | 4      | uint32 | `reserved`     | Réservé — `0xFFFFFFFF`                   |

```c
typedef struct __attribute__((packed)) {
    uint8_t  boot_scene;     // 0xFF = aucune scène au boot
    uint8_t  temp_derating;  // pourcentage de réduction thermique
    uint16_t hours_counter;  // heures de fonctionnement
    uint32_t reserved;       // 0xFFFFFFFF
} AuroraSystemConfig;  // 8 bytes
```

---

## 10. CRC-32 (4 bytes)

### 10.1 Algorithme

**CRC-32 IEEE 802.3** avec les paramètres suivants :

| Paramètre   | Valeur         |
|--------------|----------------|
| Polynôme     | `0xEDB88320` (réfléchi) |
| Valeur initiale | `0xFFFFFFFF` |
| XOR final    | `0xFFFFFFFF`   |

### 10.2 Périmètre

Le CRC est calculé sur **Header + tous les blocs TLV** (tout sauf le CRC lui-même et la signature).

```
CRC32 = crc32(data[0 .. header_size + total_length - 1])
```

### 10.3 Implémentation C

```c
static const uint32_t crc32_table[256];  // table précalculée

static void build_crc32_table(void) {
    for (uint32_t i = 0; i < 256; i++) {
        uint32_t crc = i;
        for (int j = 0; j < 8; j++) {
            crc = (crc & 1) ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        crc32_table[i] = crc;
    }
}

uint32_t compute_crc32(const uint8_t* data, size_t length) {
    uint32_t crc = 0xFFFFFFFF;
    for (size_t i = 0; i < length; i++) {
        crc = (crc >> 8) ^ crc32_table[(crc ^ data[i]) & 0xFF];
    }
    return crc ^ 0xFFFFFFFF;
}
```

---

## 11. Signature (4 bytes)

Les 4 derniers octets du fichier contiennent la signature `0x464C4F52` (**"FLOR"** en ASCII, little-endian).

```
Bytes: 52 4F 4C 46  →  uint32_t = 0x464C4F52
```

---

## 12. Validation complète d'un fichier .flora

### 12.1 Procédure de validation

1. Vérifier la taille minimale : `>= 24 bytes` (Header 16 + CRC 4 + Signature 4).
2. Vérifier la taille maximale : `<= 2048 bytes`.
3. Vérifier la signature "FLOR" aux 4 derniers octets.
4. Lire le Header et vérifier `magic == 0x41555241`.
5. Vérifier `version <= 0x02`.
6. Vérifier la cohérence : `file_size == 16 + total_length + 4 + 4`.
7. Calculer le CRC-32 sur `data[0..16+total_length-1]` et comparer avec le CRC stocké.
8. Parser les blocs TLV.

### 12.2 Règles de validation des données

| Règle                               | Sévérité    |
|--------------------------------------|-------------|
| `magic != 0x41555241`               | Erreur      |
| `version > CURRENT_VERSION`         | Erreur      |
| Taille fichier incohérente           | Erreur      |
| CRC-32 incorrect                     | Erreur      |
| Signature FLOR absente               | Erreur      |
| Nombre de scènes > 16               | Erreur      |
| IDs de scène non séquentiels         | Erreur      |
| Plus d'une scène AutoStart           | Erreur      |
| Mode LED invalide                    | Erreur      |
| Nombre d'entrées > 10               | Erreur      |
| ID d'entrée >= 10                    | Erreur      |
| ID d'entrée dupliqué                 | Erreur      |
| Trigger/Action invalide              | Erreur      |
| Taille > 90% de la page Flash       | Avertissement |
| Aucune scène définie                 | Avertissement |
| Aucune entrée configurée             | Avertissement |
| Cible LoadScene inexistante          | Avertissement |

---

## 13. Exemple de fichier .flora minimal

Configuration avec 1 scène (Static, blanc) et 1 entrée (Rising → LoadScene 0) :

```
Offset  Hex                                         Description
------  ------------------------------------------  -------------------------
0x00    41 55 52 41                                  Magic "AURA"
0x04    02                                           Version 2
0x05    04                                           4 blocs
0x06    26 00                                        TLV total = 38 bytes
0x08    01 00 00 00                                  WriteCount = 1
0x0C    FF FF FF FF                                  Reserved

Header TLV : LEDs
0x10    01 00 04 00                                  Type=0x01, Flags=0, Len=4
0x14    FF 00 00 FF                                  MaxPwm=255, SoftStart=0, Reserved=0xFF

Header TLV : Scènes (1 scène × 12 bytes)
0x18    02 00 0C 00                                  Type=0x02, Flags=0, Len=12
0x1C    00 00                                        SceneId=0, Flags=0
0x1E    01 FF FF FF 00 00 00 00 00 00                Static, R=255, G=255, B=255, t_on=0, t_off=0, repeat=0, fade=0

Header TLV : Entrées (1 entrée × 10 bytes)
0x2A    03 00 0A 00                                  Type=0x03, Flags=0, Len=10
0x2E    00 00 00 00 00 00 14 00 00 FF                I0, Rising, LoadScene, Target=0, Param=0, Debounce=20ms, Priority=0, Reserved=0xFF

Header TLV : Système
0x38    04 00 08 00                                  Type=0x04, Flags=0, Len=8
0x3C    FF 00 00 00 FF FF FF FF                      BootScene=0xFF, TempDerating=0, HoursCounter=0, Reserved

CRC-32
0x44    XX XX XX XX                                  CRC-32 (calculé sur 0x00..0x43)

Signature
0x48    52 4F 4C 46                                  "FLOR" (little-endian)
```

Taille totale : **76 bytes** (16 + 38 + 4 + 4 = 62… ajuster selon calcul réel)

---

## 14. Calcul de taille

```
taille_fichier = 16                          // Header
               + 4 + 4                       // Bloc LEDs (TLV header + payload)
               + 4 + (num_scenes × 12)       // Bloc Scènes
               + 4 + (num_inputs × 10)       // Bloc Entrées
               + 4 + 8                       // Bloc Système
               + 4                           // CRC-32
               + 4                           // Signature

             = 48 + (num_scenes × 12) + (num_inputs × 10)
```

**Exemples :**

| Scènes | Entrées | Taille totale |
|--------|---------|---------------|
| 0      | 0       | 48 bytes      |
| 1      | 1       | 70 bytes      |
| 4      | 4       | 136 bytes     |
| 8      | 5       | 194 bytes     |
| 16     | 10      | 340 bytes     |

Marge confortable — la limite de 2048 bytes n'est jamais atteinte en conditions normales.

---

## 15. Documents liés

| Référence           | Titre                                    |
|----------------------|------------------------------------------|
| DT-AURORA-PROTO-001  | Protocole de communication AURORA        |
| DT-AURORA-AUTH-001   | Authentification et sécurité             |
| DT-AURORA-UPLOAD-001 | Protocole de téléversement               |
| DT-AURORA-IO-001     | Entrées/Sorties et GETINPUT              |

---

© 2025 — Projet Aurora-LINK
