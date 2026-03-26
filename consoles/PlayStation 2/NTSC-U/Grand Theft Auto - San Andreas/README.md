# Grand Theft Auto - San Andreas

**Console:** PlayStation 2  
**Region:** NTSC-U  
**Serial:** SLUS-20946  
**Source:** [gamehacking.org](https://gamehacking.org/game/44)

---

## Cheats

### Master Code [NTSC-U]
**Author:** ReallyCoolName  
**Notes:** Must be active for all other codes to work.

```
F0100008 0000000E
```

---

### Infinite Health
**Author:** GodModeGuy

```
2A456B10 000000C8
```

---

### Infinite Ammo
**Author:** GodModeGuy

```
2058B650 05F5E0FF
2058B654 05F5E0FF
```

---

### All Weapons (Pointer-based)
**Author:** PointerPro  
**Notes:** Follows a pointer chain to the player weapon slot.

**Pointer chain:**
```
Base:      203F8A00 00000000
Offset 1:  +0x10
Offset 2:  +0x3C
Final:     1058B600 00000009
```

---

### Wanted Level — Never Wanted
**Author:** LawlessCoder

```
204A5680 00000000
```

---

## Patches

| Patch | Description |
|---|---|
| [NTSC-U Patch v1.0](./patches/NTSC-U-Patch-v1.0/) | Fan-made bugfix patch; cheats adjusted for patched ELF |
