[English] · [Русский](README.ru.md)

# BCF vocabularies — the single source of truth

This folder defines the agreed values for `TopicType`, `TopicStatus`,
`Priority`, `TopicLabel`, `Stage`, and the mapping of clash statuses coming
from a clash-detection tool.

**Only `bcf-extensions.json` is edited.** Everything else is derived from it.

## Contents

| File | Purpose |
|---|---|
| `bcf-extensions.json` | The canonical vocabulary: wire values, display labels, semantics, colours, allowed transitions, mapping tables |
| `extensions.xml` | A ready file for the root of a `.bcfzip` (BCF 3.0) |
| `README.md` | Rules, reasoning, integration notes |

## The key decision: wire values are English

BCF has **no pair of "identifier plus display name"**. `TopicStatus` in
`markup.bcf` is an ordinary string, and third-party applications — Solibri,
BIMcollab Zoom, Revizto — show it to the user literally.

Therefore:

- Machine values are English ASCII: `In Progress`, `Clash`, `Critical`.
- Localised labels are used **in a user interface only**, through the
  `labels` table.
- Non-ASCII text never reaches a file or an HTTP body.

The opposite choice — localised values on the wire — looks convenient on the
first day and breaks on the second: other parsers stumble over encodings and
your data stops being portable.

## Rules for changing the vocabulary

1. **Wire values are immutable after the first release.** Renaming a status
   means migrating every topic on the server and diverging from archives
   already sent out.
2. **You may add, you may not remove.** A value that fell out of use is
   marked `deprecated: true`, hidden in the UI when creating, and still
   displayed correctly in older data.
3. **The vocabulary version** (`vocabularyVersion`) is semver. Major for
   incompatible changes, minor when values are added. A service should hand
   it to its clients so that a client can warn about a mismatch.
4. Comparison is **strict, case and spaces included**. `In Progress` ≠
   `in progress` ≠ `InProgress`.

## The lifecycle model

```
New ──► Assigned ──► In Progress ──► Resolved ──► Closed
 │          │             │              │           │
 │          │             │              └─► Reopened┘
 │          │             │                     │
 └──────────┴─────────────┴─► Deferred ◄────────┘
 └──────────┴─────────────────► Rejected
```

Decisions worth understanding:

- **`Resolved` is not terminal.** The assignee claims the issue is fixed;
  the coordinator confirms it after the next model export. Merging "the
  assignee said so" and "it was verified" into one status is the most common
  mistake in systems like this: the metrics stop reflecting reality.
- **`Reopened` is a status of its own, not a return to `Assigned`.** It
  exists for the re-opening metric: the share of reopened issues says more
  about a discipline's quality than the total number of clashes.
- **`Rejected` and `Deferred` require a comment** (`requiresComment: true`).
  Closing without a reason is what devalues the system a month into use.
- **`Assigned` and `In Progress` require an assignee**
  (`requiresAssignee: true`).

`allowedTransitions` is meant for validation on a server. A client may use it
to avoid offering impossible transitions, but **the authoritative check
belongs to the server**: clients cannot be trusted, and third-party clients
know nothing about your transitions at all.

## An ambiguity that each deployment resolves for itself

The `Approved` status of a clash-detection tool is used in two different
ways:

| Reading | Target BCF status | When to choose it |
|---|---|---|
| "The fix was verified and accepted" | `Closed` (the current default) | The coordinator runs the whole cycle in the clash tool |
| "The intersection was accepted as tolerable" | `Rejected` | `Approved` is used as an agreed deviation |

The mapping is overridable in the export settings; the default should match
what your coordinators actually do. `bcf-extensions.json` currently says
`Closed`.

## The asymmetry of mapping back

The status set of a clash-detection tool (`New`, `Active`, `Reviewed`,
`Approved`, `Resolved`) is poorer than the one above. Pulling statuses back
from a coordination service therefore loses information: `Assigned`,
`In Progress` and `Reopened` all collapse into `Active`.

The consequence: **a clash-detection tool cannot be the source of truth for
statuses.** The full status lives on the server; what is written back into
the clash result is a convenience for whoever looks only at the native
interface. Do not build logic on it.

## The asymmetry of validation: strict out, lenient in

The standard does not fix the vocabularies — each implementation declares its
own. That means **incoming files from other tools will contain values that
are not in this vocabulary**: BCF from Revizto, BIMcollab or Solibri arrives
with its own statuses and types, entirely legitimately.

Two different modes follow:

| Direction | Behaviour |
|---|---|
| **Outgoing** (creating topics, POST to an API, writing a file) | Strict validation. A value outside the vocabulary is an error — `422` with the allowed values listed |
| **Incoming** (importing a `.bcfzip`, accepting topics from other clients) | The value is preserved **as-is** and the topic is not rejected. A UI shows it marked as external, in a neutral colour, and it takes no part in transition validation |

On a server:

- Store `topic_status` as a string, not as a reference to an enum table.
  A foreign key onto the vocabulary will trap you on the first import from
  outside.
- Keep a separate computed flag such as `is_known_status` — metrics and
  filters are built on it so that external values do not distort statistics.
- Give the coordinator a way to **map values by hand**: "`Открыто` from
  someone else's file → our `New`", preserving the original value in the
  topic history.
- The same applies to `topic_type`, `priority`, `labels` and `stage`.

Rejecting an import because of an unfamiliar status is the fastest way to
earn a reputation as the tool that "does not understand openBIM".

## Integration: a service (Node/TypeScript)

1. Keep `bcf-extensions.json` in the repository as the only source of
   vocabularies. Do not create parallel enums in code.
2. Generate types and constants at build time:

```ts
import vocab from './bcf-extensions.json';

export const TOPIC_STATUSES = vocab.topicStatuses.map(s => s.value);
export type TopicStatus = (typeof vocab.topicStatuses)[number]['value'];

export const TRANSITIONS = Object.fromEntries(
  vocab.topicStatuses.map(s => [s.value, s.allowedTransitions])
);
```

3. Serve `GET /bcf/3.0/projects/{id}/extensions` **from this same file**
   rather than from a hard-coded response. Otherwise the file channel and the
   API channel are guaranteed to diverge.
4. Validate `topic_status`, `topic_type`, `priority`, `labels` and `stage`
   **on creation and modification through your own clients** — respond `422`
   with the allowed values for anything outside the vocabulary. On **file
   import and on topics from third-party clients** do not validate: store the
   value as-is (see the section on validation asymmetry).
5. Validate transitions against `allowedTransitions` and reject impossible
   ones with `409`.
6. Check `requiresComment` and `requiresAssignee` before a status change.

## Integration: a .NET host

1. `bcf-extensions.json` is already an embedded resource of `Bcf.Core`.
2. Constants are generated from it — **never write the strings by hand at
   the call site**. See `Bcf.Vocabulary.Generator`.
3. Sensible export defaults: `TopicType = Clash`, `Priority = Normal`,
   `Stage = Design`, and the status through `navisworksStatusMapping`.
4. Derive a discipline label (`ARC`, `HVAC`, …) from the name of the clash
   test or from the selected sets, with a manual override available.
5. Put the `Auto` label on everything exported automatically: it lets a
   receiving service tell automatic clashes from issues raised by hand
   without parsing any text.
