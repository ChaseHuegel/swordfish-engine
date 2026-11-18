# Improvement: Scale the UI for high-density windows

- Type: improvement
- Status: closed
- Workflow: ../specs/issues.md

## Problem

Reef draws in raw window pixels. The settings page keeps fixed font sizes and
fixed metrics, so it stays at 1280 by 840 pixels on a 4K window. Text becomes
physically small as the window grows. Other menus have the same problem.

## Acceptance criteria

- [x] A global UI scale exists or Reef renders into a virtual-resolution buffer
      that scales to the window.
- [x] The settings page and the other menus stay readable from 720p through 4K.
- [x] Input coordinates map to the scaled UI correctly.
- [x] The change does not break the pixel-art crispness of the existing font
      atlas.
