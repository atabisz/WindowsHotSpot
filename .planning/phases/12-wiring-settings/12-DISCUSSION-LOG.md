# Phase 12: Wiring + Settings - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-06
**Phase:** 12-Wiring + Settings
**Areas discussed:** Settings UI layout, Step control range, Unit label

---

## Settings UI Layout

| Option | Description | Selected |
|--------|-------------|----------|
| Expand existing group | Add second row inside "Window Interactions" group (height 48→78px) | ✓ |
| Separate group | New "Window Transparency" group below "Window Interactions" | |

**User's choice:** Expand existing group
**Notes:** Keeps transparency grouped with scroll resize under one heading.

---

## Step Control Range

| Option | Description | Selected |
|--------|-------------|----------|
| 1–50, increment 1 | Matches TRNSP-04 spec exactly. Fine-grained control. | ✓ |
| 1–50, increment 5 | Coarser steps match scroll resize increment style. | |

**User's choice:** 1–50, increment 1
**Notes:** Matches REQUIREMENTS.md TRNSP-04 spec exactly.

---

## Unit Label

| Option | Description | Selected |
|--------|-------------|----------|
| α / notch | Alpha is the technical term (0–255 scale). Consistent with internals. | ✓ |
| % / notch | More user-friendly but misleading — step is raw alpha units. | |
| No label | Minimal — matches nothing else in the form. | |

**User's choice:** α / notch

---

## Claude's Discretion

- `buttonPanelTop` cascade update (from `windowInteractionsGroupTop + 56` to `+ 86`) to accommodate taller group
- Wiring sequence in HotSpotApplicationContext (after ScrollResizeHandler, before AlwaysOnTopHandler)
- WheelSuppressionPredicate OR-combination lambda

## Deferred Ideas

None — discussion stayed within phase scope.
