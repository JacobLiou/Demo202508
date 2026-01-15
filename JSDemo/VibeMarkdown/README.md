# VibeMarkdown

A modern Markdown editor built with React and Electron.

## Features

- **Markdown Editing**: Full Markdown support with real-time preview
- **Table of Contents**: Auto-generated TOC from headings
- **Split View**: Edit and preview side by side
- **Multi-tab Editing**: Open and edit multiple files simultaneously
- **Window Controls**:
  - Always on top mode
  - Snap to left/right edges
- **Editor Tools**:
  - Undo/Redo
  - Find and Replace
  - Go to Line
  - Insert Date & Time
- **Multi-language Support**: English and Chinese
- **Dark Mode**: Comfortable dark theme for night use

## Installation

```bash
npm install
```

## Development

To run the app in development mode:

```bash
npm run dev
```

This will start both the React development server and Electron.

## Building

To build the app for production:

```bash
npm run build
npm run electron
```

## Usage

### File Operations
- **New File**: `Ctrl+N` (or `Cmd+N` on Mac)
- **Open File**: `Ctrl+O` (or `Cmd+O` on Mac)
- **Save**: `Ctrl+S` (or `Cmd+S` on Mac)
- **Save As**: `Ctrl+Shift+S` (or `Cmd+Shift+S` on Mac)

### Editing
- **Undo**: `Ctrl+Z` (or `Cmd+Z` on Mac)
- **Redo**: `Ctrl+Y` (or `Cmd+Y` on Mac)
- **Find**: `Ctrl+F` (or `Cmd+F` on Mac)
- **Replace**: `Ctrl+H` (or `Cmd+H` on Mac)
- **Go to Line**: `Ctrl+G` (or `Cmd+G` on Mac)

### View
- **Toggle Dark Mode**: `Ctrl+Shift+D` (or `Cmd+Shift+D` on Mac)
- **Always On Top**: Available in View menu
- **Snap to Left/Right**: Available in View menu

## Technologies

- React 18
- Electron 27
- Marked (Markdown parser)
- DOMPurify (HTML sanitization)
- react-i18next (Internationalization)
- react-split-pane (Split view)

## License

MIT

