# 🎮 Core Gameplay Systems

← [Back to README](../README.md)

## Movement

Current movement is built on the Unity Starter Assets third-person foundation with project-specific changes.

The current movement foundation is considered good enough to continue building real gameplay.

Future tuning should be driven by real encounters, not rewritten from scratch.

Areas worth testing later:
- acceleration / deceleration
- fast direction changes
- wall glancing
- analog response
- target-locked movement
- sprint feel

---

## Camera

Current camera is an elevated 3/4 action view.

Do not lock the final production framing from the practice range alone.

After the first real level fragment exists, compare:

1. **Current**
2. **~5–10% closer**
3. **Current-ish distance + subtle smooth look-ahead toward aim**

Judge:
- battlefield readability
- movement feel
- enemy visibility
- cover
- weapon visibility
- mobile screen usage

---

## Aim / Auto-Aim

Current mobile behavior includes:
- screen visibility filtering
- real-world LOS
- center-screen priority
- sticky current target
- manual switching
- no-lock firing
- shot-accuracy zones

Planned real-combat revisit:
- tune auto-aim against actual moving enemies
- verify short-cover aim-point behavior
- test **free-look + auto-shoot** while right joystick is actively used

Do not assume auto-shoot is correct until tested.

---

## Muzzle safety

A weapon muzzle can cross a nearby wall even when the player body has not.

The firing pipeline includes a body-to-muzzle safety check.

Always test a new weapon:
- face pressed against full wall
- near corner
- behind low cover
- while moving

Do not remove this protection.

---

## Practice range

The practice range is a dev sandbox feature:
- breakable cardboard targets
- moving rails
- target shooting
- reassembly
- Hardcore damage

It is useful for tuning, but real gameplay architecture should not depend on it.

---

## Current game direction

Kids VS Aliens is intended to become a **difficult mission-based PvE action game with light roguelite progression**.

Story should unfold gradually.

Early player knowledge is intentionally limited.

Larger plot direction:
- protagonist was abducted
- aliens are not one unified faction
- an internal alien rebellion exists
- a rebel is secretly helping the protagonist
- mysterious teleportation/beaming can later be recontextualized
- story becomes increasingly twist-heavy as player power grows

Near-term playable goal:

```text
wake / arrive
↓
small exploration objective
↓
first danger
↓
improvised/melee possibility
↓
alien weapon discovery
↓
real combat encounter
↓
mysterious clue / intervention
```
