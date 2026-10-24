# Gunfire Reborn Character Sheet

Experimental Windows Steam mod showing damage totals, observed Lucky Shot Chance, cumulative build bonuses, and contributing items. Hold **C** to view; right-click while holding C to interact.

- [Install, update, or uninstall](docs/INSTALL.md)
- [Download version 0.13.50](https://github.com/tfendall/gunfire-reborn-character-sheet/releases/tag/v0.13.50)
- [Build and test](docs/DEVELOPMENT.md)
- [Experimental release notes and limitations](docs/RELEASE_NOTES.md)
- [Weapon damage: What powers this? (development)](docs/DAMAGE_POWERS.md)
- [Attribution coverage reports and captured replay tests](docs/ATTRIBUTION_COVERAGE.md)
- [Third-party components](docs/THIRD_PARTY.md)

Guided releases contain an inspectable Windows installer and our mod DLL. Mod-only releases contain our DLL and instructions. Version 0.13.38 uses HarmonyX 2.16.1 and .NET 10.0.12 with a modified BepInEx loader. Download the separate **LoaderOverlay ZIP** for that loader; **LoaderSource ZIP** supplies its corresponding source and license notices. No compiler is needed to install the overlay. EmberCloak, restored metadata, the base BepInEx package, and .NET runtime are separate upstream downloads; the installer detects prerequisites and links to their instructions.

Source is in `src/CharacterSheet`; managed calculation tests are in `tests/Managed`; installer tests use isolated temporary folders. Intermittent native startup crashes and incomplete modifier attribution remain known limitations.

<img width="359" height="724" alt="image" src="https://github.com/user-attachments/assets/24224142-d6e0-499e-9ab3-69d14a8cb2a9" />

<img width="356" height="721" alt="image" src="https://github.com/user-attachments/assets/637cbd5b-7e3d-4f0d-bcd5-baa9b885f47b" />

<img width="360" height="689" alt="image" src="https://github.com/user-attachments/assets/61b65a40-dc95-437d-8478-98671d3b4a61" />

