# Release Notes

Summary of what changed in each released version of the PEAK LAN mod.

For detailed, technical, per-change history (file/class-level notes for developers), see `CHANGELOG.md`. That file is not bundled with releases; this document is.

## 1.2.0 - 2026-09-21

- When a host leaves a room, the local LAN server now stays running by default so other players can keep playing; a config option is still available to restore the old auto-stop behavior.
- Simplified how incompatible sessions are detected in the server list; game and mod version mismatches are still flagged.
- Expanded the list of optional companion mods bundled with the release, with short descriptions of what each one does, and renamed their folder for clarity.
- Clarified several in-mod configuration descriptions to make setup easier to follow.

## 1.1.0 - 2026-09-18

- The in-game LOG panel now shows clear, plain-language events (hosting started, joined a room, left the game, failed to host/join with a reason, disconnected) instead of raw technical connection state changes.
- Host, join, and leave log entries now include the room name, whether the room is password protected, and the room owner's nickname.

## 1.0.0 - 2026-09-18

- Added optional password protection for LAN rooms, so a host can require a password before others can join.
- General installation and release package cleanup for a simpler setup experience.
- Added a small bouncing tagline on the main menu for fun.
- Fixed an issue where a recent PEAK patch handled room names differently, which would cause joins to fail.
- Various log-handling and stability improvements ahead of the 1.1.0 log panel overhaul.

## 0.7.0 - 2026-08-09

- Restyled the LAN overlay to match PEAK's native fonts and colors for a more integrated look.
- The server list now shows room occupancy (how many players are in a session and how many it can hold).
- Player customization/appearance choices are now remembered between sessions while playing offline.
- General server list polish and stability fixes, including reduced connection-log spam.

## 0.5.0 - 2026-08-07

- Simplified LAN setup: the mod now only supports the local LAN server workflow, removing the older "custom cloud" connection mode and its settings.
- Cleaned up and renamed several internal settings for clarity; existing config files are automatically migrated, so upgrading doesn't require manual changes.
- The mod's config file is now generated automatically on first launch instead of shipping a static template.
- Numerous internal reliability and maintainability improvements with no intended change to gameplay behavior.

## 0.4.0 - 2026-08-03

- Improved network setup reliability and stability.
- Improved and simplified the installation guide.
- Added separate connection settings for hosting versus joining.
- Added the mod's license file to the release package.

## 0.3.0 - 2026-08-02

- Added an in-game server list panel: see nearby LAN sessions and join one directly instead of entering connection details manually.
- The server list can now scroll and is no longer limited to a fixed number of visible sessions.
- "Join Selected" is disabled until a compatible session is chosen, and the panel explains why a session can't be joined when relevant.
- The panel now shows a "last refreshed" time and refreshes automatically, in addition to manual refresh.
- Host/Join/Refresh actions are now clickable buttons instead of keyboard shortcuts only.

## 0.2.0 - 2026-08-02

- Added automatic LAN discovery: hosts announce their session on the local network, and other players see it without needing to know an IP address.
- Sessions with a mismatched mod, game, or protocol version are automatically flagged as incompatible.
- Added optional auto-start of the local LAN server when hosting, with automatic waiting until the server is ready before connecting.
- Added automatic configuration of the local LAN server so it can be reached correctly by other players.

## 0.1.0 - 2026-07-28

- Proved out the foundation for direct PEAK multiplayer without Steam lobbies, connecting two clients over a LAN.
- Established the core LAN mode and connection groundwork used by all later releases.
