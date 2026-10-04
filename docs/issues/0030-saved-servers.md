# Improvement: Saved server list on the multiplayer page

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest: players want to save servers to the multiplayer page. Re-joining
a favorite LAN or remote server today means retyping the address and port
every session; there is no favorites concept anywhere in the client.

Locked design:

- Storage: `ProfileSettings` (`profile.toml`, established in #0029 as the
  home for client state) gains a saved-server record list - each entry
  `{ Name, Host, Port }` - persisted with the same config save path.
- Multiplayer page: a saved-servers section listing entries; clicking one
  prefills the page and connects (reusing the normal `ConnectRemote` +
  join flow); an add/remove affordance manages entries; "add" captures
  the page's current host/port with an editable name.
- Dedupe by host:port; hard cap on entries (32) so the list cannot grow
  unbounded in a config file.
- The page follows the visual conventions of other menu pages: the remove
  action is a trash-can icon button consistent with existing icon-button
  styling elsewhere in the UI.

## Acceptance criteria

- [ ] Saved servers persist across restarts via `ProfileSettings`; list
      renders on the multiplayer page with connect and remove actions.
- [ ] Add captures host/port from the page (name optional, defaults to the
      host); duplicates by host:port are replaced, and the list is capped
      at 32 entries.
- [ ] Connecting from the list uses the standard join flow and updates the
      #0028/#0029 persisted endpoint and `LastServerMode`.
- [ ] Remove uses a trash-can icon button matching existing menu-page icon
      styling.
- [ ] `docs/specs/config-schemas.md` documents the saved-server records;
      `networking-join.md` notes the saved-server connect path (docs
      pass).