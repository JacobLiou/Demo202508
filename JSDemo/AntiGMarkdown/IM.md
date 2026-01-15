Markdown Editor Implementation Plan
Goal Description
Build a modern, feature-rich Markdown editor using React and Electron. The application will support multi-tabs, real-time split-view preview, window management (always on top, snapping), and advanced editing features.

User Review Required
TailwindCSS: The system instructions discourage Tailwind unless requested, but for a "Premium Design" with complex layouts (split views, tabs), utility-first CSS is very helpful. I will stick to Vanilla CSS / CSS Modules as per instructions, but please confirm if Tailwind is preferred for speed/aesthetics.
Editor Library: I plan to use CodeMirror 6 for the editor component. It is lighter than Monaco and very extensible.
Proposed Architecture
Tech Stack
Core: Electron, React 18, Vite
Language: TypeScript (User didn't specify, but highly recommended for robustness. I'll use JS if preferred, but TS is standard. I'll stick to JS/JSX for simplicity unless strictly needed, or maybe TS if I feel confident. Let's stick to JavaScript (JSX) to keep it simple and aligned with standard prompts unless I see existing TS config). Correction: I will use JavaScript as per the "JSDemo" folder hint, but if I can use TS I will. The folder is "JSDemo", so I will stick to standard JavaScript to avoid transpilation issues if the user environment is limited, though Vite handles TS fine. I'll prioritize JavaScript.
State Management: React Context or Zustand (for Tab/File state).
Styling: CSS Modules / Vanilla CSS Variables for theming.
Markdown: react-markdown + remark-gfm + rehype-highlight (for syntax highlighting).
Editor: @uiw/react-codemirror (wrapper for CodeMirror 6).
Directory Structure
/
  package.json
  electron/
    main.js
    preload.js
  src/
    components/
      Editor/
      Preview/
      TabBar/
      Sidebar/
    hooks/
    styles/
    App.jsx
    main.jsx
Proposed Changes
Phase 1: Setup
Initialize Vite React app.
Configure Electron main process to load the Vite dev server URL in dev and file path in prod.
Setup IPC handlers for file operations.
Phase 2: Core Editor
Editor component using CodeMirror.
Preview component using react-markdown.
Split pane layout.
Phase 3: Tabs & Files
Manage state of open files (content, path, dirty flag).
IPC events for dialog.showOpenDialog, dialog.showSaveDialog.
Phase 4: Window & System
IPC events for setAlwaysOnTop, window resizing/positioning.
Menu bar integration.
Phase 5: Polish
Dark mode (CSS variables).
i18n (Context).
Verification Plan
Automated Tests
npm run test (if unit tests are added).
Build verification: npm run build followed by checking if the dist/executable launches.
Manual Verification
Launch: Verify app opens.
Editor: Type Markdown, verify preview updates.
Tabs: Open multiple files, switch tabs, close tabs.
Window: specific tests for "Always on Top" and "Snap" buttons.
Theme: Toggle dark mode.