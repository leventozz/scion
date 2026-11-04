---
name: Scion
colors:
  surface: '#131315'
  surface-dim: '#131315'
  surface-bright: '#39393b'
  surface-container-lowest: '#0e0e10'
  surface-container-low: '#1b1b1d'
  surface-container: '#201f21'
  surface-container-high: '#2a2a2c'
  surface-container-highest: '#353437'
  on-surface: '#e5e1e4'
  on-surface-variant: '#c0c9c0'
  inverse-surface: '#e5e1e4'
  inverse-on-surface: '#303032'
  outline: '#8a938b'
  outline-variant: '#414942'
  surface-tint: '#9cd3af'
  primary: '#9cd3af'
  on-primary: '#003920'
  primary-container: '#679c7b'
  on-primary-container: '#00311b'
  inverse-primary: '#35684b'
  secondary: '#b2cdbb'
  on-secondary: '#1e3528'
  secondary-container: '#344c3e'
  on-secondary-container: '#a1bbaa'
  tertiary: '#b6ccbc'
  on-tertiary: '#22342a'
  tertiary-container: '#819688'
  on-tertiary-container: '#1b2e23'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#b7efca'
  primary-fixed-dim: '#9cd3af'
  on-primary-fixed: '#002111'
  on-primary-fixed-variant: '#1b5035'
  secondary-fixed: '#cee9d6'
  secondary-fixed-dim: '#b2cdbb'
  on-secondary-fixed: '#082014'
  on-secondary-fixed-variant: '#344c3e'
  tertiary-fixed: '#d2e8d8'
  tertiary-fixed-dim: '#b6ccbc'
  on-tertiary-fixed: '#0c1f15'
  on-tertiary-fixed-variant: '#384b3f'
  background: '#131315'
  on-background: '#e5e1e4'
  surface-variant: '#353437'
  avatar-sage-bg: '#27392e'
  avatar-sage-fg: '#86b595'
  avatar-slate-bg: '#233344'
  avatar-slate-fg: '#8aaecb'
  avatar-rose-bg: '#3b2730'
  avatar-rose-fg: '#c98e9e'
  avatar-ochre-bg: '#383120'
  avatar-ochre-fg: '#c7af79'
  avatar-lavender-bg: '#2f2b42'
  avatar-lavender-fg: '#ab9ed6'
  avatar-teal-bg: '#1d3536'
  avatar-teal-fg: '#71b1ae'
  avatar-clay-bg: '#3a2924'
  avatar-clay-fg: '#c39284'
  avatar-steel-bg: '#28323a'
  avatar-steel-fg: '#8eabbd'
  avatar-moss-bg: '#2a3523'
  avatar-moss-fg: '#93af82'
typography:
  headline-xl:
    fontFamily: Newsreader
    fontSize: 38px
    fontWeight: '400'
    lineHeight: 48px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Newsreader
    fontSize: 28px
    fontWeight: '400'
    lineHeight: 38px
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Newsreader
    fontSize: 22px
    fontWeight: '500'
    lineHeight: 30px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Newsreader
    fontSize: 18px
    fontWeight: '500'
    lineHeight: 26px
    letterSpacing: 0em
  body-lg:
    fontFamily: Newsreader
    fontSize: 18px
    fontWeight: '400'
    lineHeight: 30px
    letterSpacing: 0em
  body-md:
    fontFamily: Geist
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 22px
    letterSpacing: -0.005em
  body-sm:
    fontFamily: Geist
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 18px
    letterSpacing: 0em
  label-lg:
    fontFamily: Geist
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 18px
    letterSpacing: 0.01em
  label-md:
    fontFamily: Geist
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0.04em
  label-sm:
    fontFamily: Geist
    fontSize: 10px
    fontWeight: '600'
    lineHeight: 14px
    letterSpacing: 0.08em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  gutter: 1.5rem
  margin: 2.5rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2.5rem
---

## Brand & Style

This design system embodies monastic clarity, combining the functional discipline of high-craft desktop utilities with the tactile stillness of classical print typography. Built for sustained focus, contemplative writing, and deep archival curation, it rejects superfluous analytics, floating docks, and aggressive micro-interactions.

The visual ethos reconciles Swiss utility and literary warmth:
- **Atmospheric Restraint:** Heavy negative space operates as active structure, not empty void. Screens breathe with museum-grade proportion.
- **Editorial Serenity:** Utilitarian UI controls disappear until hovered or invoked via keybindings, yielding total priority to the manuscript and catalog.
- **Native Precision:** Tactile desktop mechanics—window title bar subtlety, razor-sharp hairline dividers, and fluid native keystrokes—evoke the durability of physical stationery crafted into digital glass.

## Colors

The palette operates under low-stimulus principles, drawing deeply from deep charcoal stone, archival cream, and muted botanical sage.

### Neutral Architecture
- **Base Canvas (Dark Mode Default):** `#0f0f11` provides an ink-like, non-reflective slate foundation.
- **Surface Elevation:** Secondary structural areas utilize `#161619` and `#1e1e23` for contextual elevation without cast shadows.
- **Light Counterpart:** For light mode contexts, `#fbfbfb` functions as pristine stone parchment, paired with `#f3f3f2` for recessed areas.

### Accent & Text Hierarchy
- **Botanical Sage (`#4e8263`):** Reserved strictly for active indicators, primary call-to-actions, and focal states. It never shouts; it grounds.
- **Secondary Sage Wash (`#8ea897`):** Applied to subtle active chips, inline tags, and literary cross-references.
- **Text Tiers:**
  - High Contrast: `#f4f4f5` (92% opacity) for headers and critical reading copy.
  - Mid Contrast: `#a1a1aa` for secondary details, metadata, and shortcuts.
  - Low Contrast: `#52525b` for placeholders and subtle structural markers.
- **Borders & Dividers:** Absolute 1px hairpins using `rgba(255, 255, 255, 0.07)` in dark mode and `rgba(0, 0, 0, 0.06)` in light mode.

## Typography

The type system creates an intentional tension between technical instrument and literary review.

- **Newsreader (Editorial & Display):** Deployed for introspective titles, quote annotations, botanical definitions, and longform composition. It brings rhythmic cadence, classical proportions, and warm optical kerning. It should lean into natural italics for botanical nomenclature, timestamps, and philosophical epigraphs.
- **Geist (Interface & Engine):** Carries all utility: hotkeys, status tags, command palettes, metadata grids, and form controls. Selected for its cold, hyper-legible geometric construction and monospaced numerical alignments.
- **Geist Mono (Code):** Code blocks, inline code and the contents of skill files. It is the same Geist family (SIL OFL, bundled in `Scion.UI/wwwroot/fonts/`, covered by `Geist-OFL.txt`). Code is Geist Mono everywhere, never a system monospace.
- **Rhythm & Formatting:** Titles feature generous leading to prevent claustrophobia. Body prose preserves an ideal reading measure of 62–72 characters per line, anchored centrally on the desktop canvas.

## Layout & Spacing

This design system targets an uncompromising native desktop window frame (canonical baseline: 1100 × 720 px), deliberately eschewing complex responsive layouts in favor of deliberate focal panes.

- **Framing Model:** A single unified canvas without collapsible sidebars. Toolbars are integrated into the window frame alongside native traffic light controls (left margin: 20px).
- **Grid Architecture:** A quiet 3-column compositional plane or a single centered manuscript track:
  - Center Column: 680px maximum width for primary creation/reading.
  - Side Margins: Expansive dynamic gutters (`space-xl` / `margin`) that keep the central artifact uncluttered.
- **Vertical Rhythm:** Base unit is 4px. Structural elements stack using `space-md` (16px) and `space-lg` (24px). Section dividers maintain a generous `space-xl` (40px) cadence to preserve silence between disparate data points.

## Elevation & Depth

Visual hierarchy is achieved through pure luminance and hairline containment rather than dramatic drop shadows.

- **Zero Heavy Shadows:** Standard elevations do not use diffuse blur plumes. Depth is rendered completely flat or via an imperceptible ambient shade: `0 1px 2px rgba(0, 0, 0, 0.4)`.
- **Whisper Hairlines:** Surfaces are bounded by ultra-fine single-pixel strokes (`1px solid rgba(255, 255, 255, 0.07)`). These outlines separate inputs, floating command palettes, and lists without clutter.
- **Tonal Layering:** 
  - Canvas Level: `#0f0f11`
  - Floating Overlays (Command Menu / Context Menus): `#161619` with a subtle backdrop filter (`backdrop-filter: blur(12px)`) and a hairline frame.
  - Highlighted Items: Inset surface tint (`rgba(255, 255, 255, 0.04)`).

## Shapes

Corner geometry prioritizes crisp architectural precision over bubble-like softness.

- **Scale Factor 1 (Soft):** Structural containers, input fields, interactive list tiles, and floating sheets strictly adhere to a disciplined radius between 4px (`rounded-sm`) and 6px (`rounded-md`).
- **Pill Exceptions:** Micro-tags, inline hotkey badges, and status pills are capped at 4px or kept strictly geometric with 2px corners, avoiding circular capsule forms to retain typographic rigor.
- **Framing Consistency:** Window borders mirror standard desktop OS corners (10px–12px at window apex), with internal components maintaining sub-6px radii for an engineered, tactile feel.

## Components

Every interactive component adheres to unobtrusive utility: visible when needed, silent when idle.

### Buttons & Key Triggers
- **Primary:** Botanical Sage background (`#4e8263`), white label (`#f4f4f5`), 6px radius, `px-3.5 py-1.5`. No heavy gradients; steady color fill. Active state compresses slightly via opacity (`0.9`).
- **Subtle / Ghost:** Transparent background with `#a1a1aa` text. Gains faint background sheen (`rgba(255, 255, 255, 0.05)`) on hover.
- **Shortcut Affordance:** Buttons feature inline monospace keycaps (`⌘K`, `Esc`) rendered in `10px Geist`, enclosed in low-contrast borders.

### Input Fields & Search (Command Palette)
- **Minimalist Rules:** Zero prominent search boxes. Search exists as an unbordered horizontal row embedded in the canvas or a floating sheet.
- **Field Anatomy:** Bare slate surface, 1px perimeter line (`rgba(255, 255, 255, 0.08)`), placeholder set in Newsreader italic or Geist neutral. Focused state changes border color to muted sage (`#4e8263`) with no loud outer glow.

### Lists & Archival Items
- **Structure:** Flat horizontal rows bounded by hair-thin border bottoms. No card containers or heavy dividers.
- **Interactions:** Hovering a row triggers a subtle background shift (`rgba(255, 255, 255, 0.03)`).
- **Metadata Layout:** Left-aligned item title in `Newsreader`, right-aligned mono dates or tags in `Geist 11px`.

### Selection Controls (Checkboxes & Radios)
- **Checkboxes:** 14 × 14px squares with 3px corners. Unchecked state is a hairline outline. Checked state fills with `#4e8263` holding a crisp single-pixel white mark.
- **Radio Buttons:** Pure concentric rings, minimal stroke weight, activating a central sage pip.

### Metadata Chips & Badges
- **Form:** Compact, borderless rectangles with 3px rounded corners.
- **Palette:** Sage tint background (`rgba(78, 130, 99, 0.15)`) with pale sage text (`#8ea897`), formatted in small uppercase `label-sm` tracking.

### Modal & Command Palette
- **Placement:** Centered floating window, 580px wide.
- **Backdrop:** Pure semi-translucent void (`rgba(0, 0, 0, 0.65)` with light blur).
- **Divider:** Ultra-thin dividing hairline separates instant text queries from keyboard-navigable list results.


## Scion Design Rules (do not break)

- Names of things (projects, skills, "Personal", the word "scion") use the
  serif typeface. Paths, counts and all other metadata use the sans-serif.
- At most one primary (light mint) button per screen. Everything else is
  quiet secondary (dark surface with a hairline border, like Add project)
  or a text action.
- Text and icons are neutral (white and grays) by default. Color is
  allowed only for: avatars (the Personal folder tile counts as Personal's
  avatar), the primary button, active-state indicators (the active tab
  underline, the selected segment), checked checkboxes, amber warnings,
  text selection, the mint number of the third dictionary definition, the
  working indicator (the mint sparkle, see below) and the update action
  ("Update to 1.1.0", a mint text action beside Add project).
- Avatar color: one of nine fixed tones, `palette[FNV-1a(name) % 9]`
  (the `avatar-*` tokens above), so the grid stays calm.
- Zero counts (e.g. "Codex 0") are dimmed.
- Skill descriptions in lists are always a single line, truncated with an ellipsis.
- Diff views: added/removed lines are marked with "+"/"−" and a background
  tint, and must stay clearly distinguishable from the mint accent.
- The agent switch is a bordered segmented control; content tabs are
  underlined text tabs. They must never look alike.
- Dialogs: closed with the × at the top right and with Esc; a click on the
  backdrop does not close them (a selection must not get lost). The agent
  switch sits right next to the project picker it belongs to. "Select all",
  "Skip all" and "Overwrite all" are neutral text actions. From/To rows show
  the display name ("Personal" or the project name), never a path.
  Information that is not a problem (e.g. "Identical in B") uses the same
  line style as a warning, but neutral and dim instead of amber.
- File contents are plain Geist Mono with line numbers, never syntax-colored.
  A code language label (such as "bash") is not shown.
- The editor is the same: plain Geist Mono with line numbers, no highlighting,
  no status bar. The Edit | Preview switch is a borderless segmented control
  (like the Skip | Overwrite choice) and must not look like the agent switch or
  the tabs. "Unsaved changes" is neutral text in normal case, never uppercase.
  Save is the one primary button of the screen; "Edit" is primary on the skill
  page and a text action in the instruction file tab.
- Names in the skill file tree appear exactly as on disk (case kept, folders
  end with a slash); they are never uppercased or prettified. Files and
  folders whose names start with a dot are not listed.
- Serif is for Scion's own names and titles only. Rendered Markdown (headings
  included) is sans-serif.
- Rendered Markdown keeps a clear heading hierarchy: H1 24px, H2 19px, H3 16px,
  all larger than the 14px body text and its bold text, so a heading is never
  mistaken for a bold paragraph (and an H2 never for a bold line). H4 to H6
  share the body size, in the heading colour. A heading has more space above
  than below (H1 32/12px, H2 28/10px, H3 22/8px, H4 to H6 18/6px), so it
  belongs to what follows it. The first element of a text has no space above.
- A SKILL.md whose body is empty or only whitespace (a new skill, for
  example) shows one dim line in the middle of the reading area, "This skill
  has no instructions yet.", in the style of "This file can't be shown here."
  but without a button. The same applies to the preview while editing. A new
  skill's file is its frontmatter and one blank line, with no headings.
  *Approved by description:* there is no export for this state; the user
  approved it from this written description (and the screenshot of the built
  screen), not from a design export.
- In "What will be shared", what would have been shared but is left out is
  listed apart from the shared items, in its own dim lines below them, so it is
  never mistaken for something that is sent: "Not shared (over 32 KB): <file>"
  for a file over the limit for one file (the number is the real limit) and
  "Not shared (total limit reached): <file>" for a file within it that no longer
  fits in the total. They are shown in the short summary and in the full list
  alike. *Approved by description* (no export).
- On the skill page the description is at most three lines. When it is longer,
  a neutral "Show more" text action follows it and expands the whole text
  ("Show less" folds it back). The action is only there when the text is cut.
- A skill folder that holds nothing but SKILL.md shows no file tree; the
  reading area uses the full width.
- The skill page is at most 1200px wide. Paragraphs and lists keep a readable
  line length (about 75 characters); tables and code blocks may use the whole
  reading area. A table that is too wide scrolls sideways inside its own box;
  long paths and inline code in cells may wrap anywhere.
- The working indicator (the sparkle) is the one sign that something is being
  worked on and nothing has come back yet. It is mint, drawn as SVG (never as
  Unicode characters: fonts draw · ✢ ✳ ✶ ✻ ✽ differently), and shows these six
  shapes in this order, growing and then shrinking again (· ✢ ✳ ✶ ✻ ✽ ✻ ✶ ✳
  ✢), one frame about every 120 ms. It has no text, no logo and no provider
  name, so it stays the same for every AI provider. Where text is expected
  (the running step of Adapt with AI) it stands alone in the middle of the
  empty text box; when the first piece of text arrives it leaves the middle
  and continues, the same animation, as the cursor at the end of the growing
  text. With `prefers-reduced-motion` it does not animate and shows the still
  shape ✻. The animation is CSS only, so it costs no renders.
- The version popover opens from the wordmark on the project grid. It is as
  wide as its content and its lines never wrap: "Scion 1.0.0" (serif), one
  status line, a separator, "Check for updates", and a checkbox row "Check for
  updates when Scion starts" (a checked checkbox may be mint, like every
  checkbox). Esc and a click outside close it. A failed check shows
  "Couldn't check for updates." in dim amber with a small "Try again" in the
  status line. Release notes in the update dialog are rendered Markdown in a
  scrolling box; without notes the box says "No release notes." in dim text.
  *Approved by description* (no export for the checkbox row, "Checking…", the
  check error and "No release notes.").
- Never label anything "Locations" in the UI. Users see "projects" and
  "Personal".
- No sidebars, dashboards, statistics, tags or status bars.