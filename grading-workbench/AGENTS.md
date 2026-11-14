# Prototype Instructions

## Approved product decisions
- Follow the selected blue-white workbench and teacher-settings image references recorded in DESIGN.md.
- Inter 500, 14px / 20px for Latin and numeric UI text. Chinese glyphs use a regular-weight Chinese font, including headings.
- All subjects; default teacher profile is 全能老师. Teacher customization has all eight questionnaire sections.
- Automatic score entry is unchecked by default. Both modes press Enter after entry; first next-answer readiness check is 0.2s, retry is 0.5s.
- The record directory is shown only in Settings. Real files must be saved to the folder explicitly selected by the user.
- This iteration is a frontend prototype. Demonstration results must be visibly identified; do not claim real model calls, platform monitoring, or OS keyboard input.

Run the local server yourself and open the preview in the browser available to this environment. Do not give the user server-start instructions when you can run it.

Before making substantial visual changes, use the Product Design plugin's `get-context` skill when the visual source is unclear or no longer matches the current goal. When the user gives durable prototype-specific design feedback, preferences, or decisions, record them in `AGENTS.md`.

When implementing from a selected generated mock, treat that image as the source of truth for layout, component anatomy, density, spacing, color, typography, visible content, and hierarchy.

Build app UI in `src/`. Keep `.openai/hosting.json`, `worker/index.js`, `scripts/prepare-sites-build.mjs`, and `tests/sites-worker.test.mjs` intact so the same local prototype can be handed to Sites. Before a Sites handoff, run `npm run build` and `npm run test:sites`; the build must leave `dist/client/index.html`, `dist/server/index.js`, and `dist/.openai/hosting.json`.

- All UI text uses Inter 500 with 14px / 20px sizing, including headings, scores, tables, labels and hints. Chinese uses the regular-weight CJK fallback.

- Current desktop layout: rubric on top, student card below fills remaining height. Original image on left, OCR text on right. OCR selection button follows image picker at student header right.
- Model configuration including API keys persists encrypted with Windows DPAPI for the current user; reload on startup.

- Rubric textarea placeholder follows the saved subject. Each subject has three reference points and scoring instructions. Examples remain placeholder-only.

- Placeholder text uses light gray #a1a1aa. The original-image empty state and OCR textarea also show concise placeholders.

- OCR result is read-only, with selection and copying available; only OCR updates the recognized answer.

- Grading records feature is removed, including navigation, page and automatic recording. Grading and score entry do not require a local storage directory. The directory remains available for teacher configuration.

- Grading follows v5 direct-score flow across subjects. Accept numeric answers and point-based reference text without requiring a question stem or model-returned maximum. OCR transcribes the full selected region including questions. Keep decimal scores and short answers intact.

- Only manual OCR selection hides and restores the workbench. Capture and score submission keep it visible, never reactivate it after each operation, and focus the grading platform only when necessary. First next-answer check waits 200 ms; retries remain 500 ms.

- Stop immediately aborts frontend waits, cancels native work, suppresses late results and gates restart on native cancellation acknowledgement. OCR structured JSON is normalized into readable text before display and grading.

- OCR uses lossless PNG screenshots, 262144 minimum and 6422528 maximum image pixels, and the documented plain-text recognition prompt. Parse all text blocks even when a parent also contains title text.

- OCR display removes LaTeX math delimiters, converts common math operators and preserves block-formula line breaks. Preserve original numeric answers, including incorrect calculations.

- Settings UI is compact: no duplicate page heading, one model panel, paired provider/model fields, compact teacher navigation and a single storage directory row.
