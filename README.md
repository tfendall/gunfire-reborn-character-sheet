# Gunfire Reborn Character Sheet

Experimental Windows Steam mod showing damage totals, observed Lucky Shot Chance, cumulative build bonuses, and contributing items. Hold **C** to view; right-click while holding C to interact.

- [Install, update, or uninstall](docs/INSTALL.md)
- [Build and test](docs/DEVELOPMENT.md)
- [Experimental release notes and limitations](docs/RELEASE_NOTES.md)
- [Third-party components](docs/THIRD_PARTY.md)

Guided releases contain an inspectable Windows installer and our mod DLL. Mod-only releases contain just our DLL and instructions. Prerequisites are detected and linked, not bundled or automatically installed.

Source is in `src/CharacterSheet`; managed calculation tests are in `tests/Managed`; installer tests use isolated temporary folders. Intermittent native startup crashes and incomplete modifier attribution remain known limitations.

