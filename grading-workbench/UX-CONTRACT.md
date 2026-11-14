# UI ownership and behavior

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
|---|---|---|---|---|
| Select/Listbox | src/ui.jsx Select | DESIGN.md | native OS popup | browser keyboard and selection |
| Form | src/ui.jsx Field, Multiline | this contract | input, select, multiline; preserved drafts | browser validation and draft retention |
| Scrollbar | src/app.css | DESIGN.md | global, forced colors | browser narrow viewport |
| Toast | App notification region | this contract | status text, no secrets | browser feedback |
| CRUD | src/storage.js and App | this contract | profiles and records; no destructive actions | node storage failure tests and browser draft checks |

Settings/teacher drafts stay in React state when switching pages. Profile edits are applied only on Save, and are stored in the selected local folder before success is announced. No folder: allow draft editing and explicit demo, explain that directory selection is needed for durable saving. Browser cannot choose a directory: show a clear compatibility error, never pretend to save.

Selected folder data is loaded before it becomes active. A write failure retains the draft and existing in-memory saved configuration. API keys remain in memory only. File attachment copying is bounded, filenames sanitized and samples not sent to services.

Workbench: automatic fill default false; demo grading is explicitly labeled and cannot type into another app. Normal frontend result confirmation records a simulated action labeled 演示确认, not 已实际填分. Local records display effective teacher/config snapshots and can be inspected/exported. Filtering/paging is local, page resets on query changes, empty/no-results distinguished.

Next-page detection is a documented future native integration, first poll .2s then .5s, not implemented by a timer pretending that a page loaded. Loading blanks never become 0 automatically. Unknown/uncertain answers require teacher review.

Dialog: native HTML dialog, showModal, Escape and cancel, browser focus trap and restoration; no browser alert/confirm/prompt. Navigation never submits external grading results.
