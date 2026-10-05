# Research: Validate LAN discovery broadcast/scan correctness on standard networks

- Type: research
- Status: done
- Workflow: ../specs/issues.md

## Problem

Playtest: LAN discovery doesn't work over tailscale. Decision (locked):
overlay networks that don't forward UDP broadcast are out of scope - no
unicast/multicast fallback work. Instead, validate that the beacon/scan
path is correct on normal broadcast-capable networks, and fix anything
defective; if sound, document the finding and take no action.

## Review scope

- Beacon: unbound `UdpClient` with `EnableBroadcast = true` targeting the
  `255.255.255.255` limited-broadcast address (`LanHost.cs:99-115,118-154`);
  permanent kill on `SocketException`; 100 ms-granularity cadence;
  `PlayerCount` read live from the hub.
- Scanner: `new UdpClient(DiscoveryPort)` bind
  (`LanDiscoveryService.cs:51-59`), scan window, endpoint dedupe,
  protocol-version filter, own-host suppression; behavior when the bind
  fails (currently `yield break`, silently disabling the page's
  discovery).
- Candidate defect points to verify: limited vs directed broadcast across
  typical home-router configurations; two game instances on one machine
  contending for the discovery port (second bind throws -> discovery
  silently disabled - `SO_REUSEADDR`/exclusive-address concerns); beacon
  sender on an ephemeral source port while this machine's own scanner
  holds 47777; any user-visible indication when discovery is unavailable.

## Review findings (recorded)

1. **Limited broadcast (255.255.255.255)** — correct as designed for
   broadcast-capable home networks and routers; the tailscale/overlay
   non-forwarding case is out of scope per the locked decision. No change.
2. **Two game instances contending for the discovery port** — the second
   `UdpClient(DiscoveryPort)` bind throws a `SocketException`; the scan
   ended immediately and the page showed "No LAN servers found", a
   misleading reading of a bind failure. **Defect: fixed.** The service
   now surfaces `LanDiscoveryService.DiscoveryUnavailable`, and the page
   shows a dedicated "LAN discovery is unavailable (the discovery port is
   in use)" message instead of the empty-scan text.
3. **Beacon source port vs this machine's scanner** — the beacon sender is
   unbound (ephemeral source) while the scanner holds 47777, so both can
   coexist on one machine; own-host suppression filters the loop. No
   change.
4. **User-visible indication** — now covered by the bind-failure message
   in (2).

## Acceptance criteria

- [x] A written review (recorded in this issue or a short doc) covering
      the scope, with verdicts on each defect point.
- [x] Defects found are fixed (and `networking-transports.md` updated); if
      nothing defective is found, the LAN discovery behavior is documented
      as designed and the issue is closed with no change.