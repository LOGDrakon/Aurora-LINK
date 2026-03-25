# DT-AURORA-IO-001 — Entrées/Sorties et GETINPUT

**Révision :** A  
**Date :** 2025-07  
**Portée :** Firmware STM32 + Application PC Aurora-LINK  

---

## 1. Introduction

Ce document décrit le mécanisme de notification des états des entrées physiques du module Aurora vers le logiciel PC. Le module dispose de **10 entrées logiques** (I0 à I9) dont l'état est communiqué via la trame poussée `GETINPUT`.

---

## 2. Caractéristiques des entrées

| Paramètre        | Valeur                    |
|--------------------|---------------------------|
| Nombre d'entrées  | 10 (I0 à I9)              |
| États possibles   | `0` (OFF) / `1` (ON)     |
| Type de signal     | Logique (GPIO)            |
| Anti-rebond par défaut | 20 ms                |

---

## 3. Trame GETINPUT

### 3.1 Direction

**Device → PC** (trame poussée, non sollicitée par le client)

### 3.2 Format

```
LINK\x1fAURORA\x1fGETINPUT\x1f<payload>\0
```

### 3.3 Payload

Le payload est une chaîne de **10 caractères**, chaque caractère représentant l'état d'une entrée :

```
Position :  0  1  2  3  4  5  6  7  8  9
Entrée   : I0 I1 I2 I3 I4 I5 I6 I7 I8 I9
Valeur   : '0' = OFF, '1' = ON
```

**Exemple :** `"0100000000"` → seule I1 est ON, toutes les autres sont OFF.

### 3.4 Taille de la trame

```
"LINK" + \x1f + "AURORA" + \x1f + "GETINPUT" + \x1f + "0000000000" + \0
= 4 + 1 + 6 + 1 + 8 + 1 + 10 + 1 = 32 bytes
```

La trame tient dans un seul paquet USB FS (< 64 bytes). Pas de fragmentation nécessaire.

---

## 4. Événements déclencheurs

### 4.1 Envoi initial après authentification

Immédiatement après une authentification réussie (`AUTH OK`), le device envoie **une trame `GETINPUT`** avec l'état courant de toutes les entrées. Cela permet au logiciel PC d'initialiser son affichage.

### 4.2 Envoi sur changement d'état

Chaque fois qu'une entrée physique change d'état (après anti-rebond), le device envoie une nouvelle trame `GETINPUT` avec l'état complet des 10 entrées.

> **Important :** La trame contient toujours l'état des **10 entrées**, pas seulement celle qui a changé. Cela simplifie le parsing côté PC et évite les problèmes de synchronisation.

### 4.3 Conditions d'envoi

| Condition                      | GETINPUT envoyé ? |
|---------------------------------|--------------------|
| Après AUTH OK                  | ✅ Oui             |
| Changement d'état GPIO         | ✅ Oui             |
| Device verrouillé              | ❌ Non             |
| Pas de client connecté          | ❌ Non             |
| Polling périodique              | ❌ Non (événementiel uniquement) |

---

## 5. Implémentation STM32

### 5.1 Lecture des GPIOs

```c
#define INPUT_COUNT 10

// Table de correspondance entrée logique → GPIO
static const struct {
    GPIO_TypeDef* port;
    uint16_t      pin;
} input_gpio_map[INPUT_COUNT] = {
    { GPIOA, GPIO_PIN_0  },  // I0
    { GPIOA, GPIO_PIN_1  },  // I1
    { GPIOA, GPIO_PIN_2  },  // I2
    { GPIOA, GPIO_PIN_3  },  // I3
    { GPIOB, GPIO_PIN_0  },  // I4
    { GPIOB, GPIO_PIN_1  },  // I5
    { GPIOB, GPIO_PIN_2  },  // I6
    { GPIOB, GPIO_PIN_3  },  // I7
    { GPIOB, GPIO_PIN_4  },  // I8
    { GPIOB, GPIO_PIN_5  },  // I9
};

static bool input_states[INPUT_COUNT] = { false };
```

### 5.2 Construction du payload

```c
void build_input_payload(char* payload) {
    for (int i = 0; i < INPUT_COUNT; i++) {
        payload[i] = input_states[i] ? '1' : '0';
    }
    payload[INPUT_COUNT] = '\0';
}
```

### 5.3 Envoi de la trame

```c
void push_input_state(void) {
    if (!device_connected) return;
    
    char payload[INPUT_COUNT + 1];
    build_input_payload(payload);
    
    // Construire et envoyer : LINK\x1fAURORA\x1fGETINPUT\x1f<payload>\0
    link_send_push("GETINPUT", payload, NULL);
}
```

### 5.4 Détection de changement avec anti-rebond

```c
#define DEBOUNCE_MS 20

static uint32_t last_change_tick[INPUT_COUNT] = { 0 };

void poll_inputs(void) {
    uint32_t now = HAL_GetTick();
    bool changed = false;
    
    for (int i = 0; i < INPUT_COUNT; i++) {
        bool current = HAL_GPIO_ReadPin(
            input_gpio_map[i].port, input_gpio_map[i].pin) == GPIO_PIN_SET;
        
        if (current != input_states[i]) {
            if (now - last_change_tick[i] >= DEBOUNCE_MS) {
                input_states[i] = current;
                last_change_tick[i] = now;
                changed = true;
            }
        }
    }
    
    if (changed) {
        push_input_state();
    }
}
```

> **Note :** `poll_inputs()` doit être appelé régulièrement dans la boucle principale ou via un timer. Fréquence recommandée : **toutes les 5 ms**.

### 5.5 Détection avancée des triggers

Pour les entrées configurées dans le fichier `.flora`, le firmware doit implémenter la détection des triggers définis dans DT-AURORA-MEM-001 §8.3 :

```c
typedef struct {
    bool     previous;
    bool     current;
    uint32_t press_start_tick;
    uint8_t  tap_count;
    uint32_t last_tap_tick;
} InputDetector;

static InputDetector detectors[INPUT_COUNT];

AuroraTrigger detect_trigger(int input_id) {
    InputDetector* d = &detectors[input_id];
    bool prev = d->previous;
    bool curr = d->current;
    uint32_t now = HAL_GetTick();
    
    // Rising edge
    if (!prev && curr) {
        d->press_start_tick = now;
        d->tap_count++;
        return TRIGGER_RISING;
    }
    
    // Falling edge
    if (prev && !curr) {
        return TRIGGER_FALLING;
    }
    
    // High level
    if (curr) {
        // Long press detection (> 1000 ms)
        if (now - d->press_start_tick > 1000) {
            return TRIGGER_LONG_PRESS;
        }
        return TRIGGER_HIGH;
    }
    
    // Low level
    if (!curr) {
        // Double tap detection (2 taps within 400 ms)
        if (d->tap_count >= 2 && now - d->last_tap_tick < 400) {
            d->tap_count = 0;
            return TRIGGER_DOUBLE_TAP;
        }
        return TRIGGER_LOW;
    }
    
    return TRIGGER_LOW;
}
```

---

## 6. Réception côté PC (Aurora-LINK)

### 6.1 Écoute des trames

Le logiciel PC s'abonne aux trames reçues sur le transport LINK :

```csharp
transport.FrameReceived += OnFrameReceived;

void OnFrameReceived(LinkFrame frame)
{
    if (frame.AppId != "AURORA" || frame.Command != "GETINPUT")
        return;
    
    string payload = frame.Arguments.FirstOrDefault() ?? "";
    // payload = "0100000000" → 10 caractères
    
    for (int i = 0; i < 10; i++)
    {
        bool isHigh = i < payload.Length && payload[i] == '1';
        UpdateIndicator(i, isHigh);
    }
}
```

### 6.2 Affichage

Le Dashboard affiche 10 indicateurs (ellipses colorées) :

- **Gris** = OFF (`payload[i] == '0'`)
- **Vert** = ON (`payload[i] == '1'`)

---

## 7. Exécution des actions

Lorsqu'un trigger est détecté sur une entrée configurée dans le `.flora`, le firmware doit exécuter l'action associée :

| Action       | Comportement firmware                              |
|--------------|-----------------------------------------------------|
| `LoadScene`  | Charger la scène `target` et appliquer son état LED |
| `SetBright`  | Régler le PWM des LEDs à `target` (0–255)          |
| `Toggle`     | Basculer l'état ON/OFF de la scène courante         |
| `AllOff`     | Éteindre toutes les LEDs (mode OFF)                |
| `DimUp`      | Augmenter le PWM de `param` pas                     |
| `DimDown`    | Diminuer le PWM de `param` pas                      |
| `Lock`       | Verrouiller le device                               |
| `Unlock`     | Déverrouiller le device                             |
| `Identify`   | Clignotement rapide d'identification (3 flashs)    |

### 7.1 Priorité

Si plusieurs entrées déclenchent simultanément, l'action avec la **plus petite valeur de `priority`** est exécutée en premier (0 = priorité la plus haute).

---

## 8. Documents liés

| Référence           | Titre                                    |
|----------------------|------------------------------------------|
| DT-AURORA-PROTO-001  | Protocole de communication AURORA        |
| DT-AURORA-AUTH-001   | Authentification et sécurité             |
| DT-AURORA-MEM-001    | Format mémoire .flora                    |
| DT-AURORA-UPLOAD-001 | Protocole de téléversement               |

---

© 2025 — Projet Aurora-LINK
