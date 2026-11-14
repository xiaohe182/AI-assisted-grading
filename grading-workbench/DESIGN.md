---
version: alpha
name: 阅卷台
description: 蓝白双栏的全科目 AI 辅助阅卷工作台
colors:
  primary: "#1648ef"
  background: "#fafbff"
  surface: "#ffffff"
  sidebar: "#eef2fb"
  text: "#172443"
  muted: "#687795"
  border: "#dce3f1"
  success: "#16836d"
  danger: "#b53b4b"
typography:
  sans:
    fontFamily: "Inter, Microsoft YaHei, sans-serif"
  chinese:
    fontFamily: "Microsoft YaHei, PingFang SC, sans-serif"
rounded:
  DEFAULT: "0.5rem"
  sm: "0.375rem"
spacing:
  section-gap: "1.5rem"
  page-max: "90rem"
components:
  button:
    minHeight: "2.5rem"
  field:
    minHeight: "2.5rem"
---

# Visual reference

Workbench: `C:/Users/huanghe/.codex/generated_images/01a1d7f7-97ed-7de1-b5e0-bef62ef6cbed/exec-20eda72d-eac4-4116-aa1d-abfdea37551e.png`.
Teacher settings: sibling `exec-113dd40e-d510-4823-adad-e14b25bd654c.png` in the same folder.
These are the chosen layout, not a request for a new aesthetic.

## Context and typography

Chinese teachers on Windows, zh-CN. Product register. Side-by-side student answer and rubric; broad paper preview and restrained score/result band. No marketing hero or unrelated analytics.
The user's latest typography overrides the heavy generated titles: Chinese regular 400; Inter 500 for Latin/numbers, 14px body and 20px line height. Heading 30px/42px, normal-weight Chinese. Numeric score may use 56px as a display role; table numerals remain 14px/20px. The Chinese font is a separate unicode-range face at weight 500 mapping to local regular fonts, preventing synthetic Chinese bold inside mixed text.

## Runtime ownership

Model B: `src/app.css :root` is canonical runtime tokens, mirrored above. Shared components in `src/ui.jsx` own fields, native selects, buttons and dialogs. Phosphor regular icons; no custom icon drawings. Native select popup geometry is intentionally OS-owned.

## Layout and states

Desktop sidebar 190px, main page padding 36px, workbench two equal columns and divider. App base surface, no nested cards. Subtle blue selection, white paper area. At 1100px reduce padding and sidebar; below 850px use compact nav and one-column content. Forms scroll naturally, footer does not obscure content.
Buttons: solid primary, outline secondary, transparent quiet. All have hover, focus, disabled/busy states and stable size. Textareas cannot resize; long forms have sufficient height and natural scrolling. Global visible scrollbar tokens; reduced-motion respected.

## Content

All live connections and OS automation remain explicitly unconnected in this frontend iteration. Demonstration math paper and result are labeled 示例. Save paths appear only inside Settings. Never show credentials in notifications, exports, teacher profiles or record files.

## Current desktop workbench

User-approved update: reference and scoring text above the student card. Student card fills remaining height, original image left and OCR text right. Image picker followed by OCR selection button at header right. Use existing field, button, card and typography tokens. Model configuration is saved locally using Windows current-user encryption and restored at startup.

## Compact settings

Settings uses a compact tab bar and a single model panel, with provider/model paired and full-width endpoint/key fields. Teacher sections use a 150px navigation column and 14px form gaps. Storage is one compact directory row. Shared typography, control tokens and top toasts remain canonical.
