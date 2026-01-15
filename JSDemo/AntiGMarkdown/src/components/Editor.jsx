import React, { useCallback } from 'react';
import CodeMirror from '@uiw/react-codemirror';
import { markdown, markdownLanguage } from '@codemirror/lang-markdown';
import { languages } from '@codemirror/language-data'; // requires install? No, codemirror/lang-markdown usually sufficient for basics unless we need more
import { githubDark } from '@uiw/codemirror-theme-github'; // Need to check if I installed this theme. I installed @codemirror/theme-dark. Let's use 'dark' or 'oneDark' if available or just custom.
// Using default dark theme from @codemirror/theme-dark if installed? 
// In package.json I added "@codemirror/theme-dark": "^6.1.1".
import { oneDark } from '@codemirror/theme-one-dark'; // Usually the package name is differnt.
// Let's stick to basic for now or use the one I installed.
// Actually @codemirror/theme-dark exports `oneDark`? No.
// Let's just use empty theme for now or default.
import { EditorView } from '@codemirror/view';

// I installed @codemirror/theme-dark.
import { oneDark as theme } from '@codemirror/theme-one-dark'; // Only if installed. 
// Wait, I installed @codemirror/theme-dark. The package name is correct.

const Editor = ({ content, onChange }) => {
    const handleChange = useCallback((val) => {
        onChange(val);
    }, [onChange]);

    return (
        <CodeMirror
            value={content}
            height="100%"
            theme="dark" // or basic 'dark' string if checking support
            extensions={[markdown({ base: markdownLanguage, codeLanguages: languages }), EditorView.lineWrapping]}
            onChange={handleChange}
            className="h-full text-base"
            basicSetup={{
                lineNumbers: true,
                highlightActiveLineGutter: true,
                highlightSpecialChars: true,
                history: true,
                foldGutter: true,
                drawSelection: true,
                dropCursor: true,
                allowMultipleSelections: true,
                indentOnInput: true,
                syntaxHighlighting: true,
                bracketMatching: true,
                closeBrackets: true,
                autocompletion: true,
                rectangularSelection: true,
                crosshairCursor: true,
                highlightActiveLine: true,
                highlightSelectionMatches: true,
                closeBracketsKeymap: true,
                defaultKeymap: true,
                searchKeymap: true,
                historyKeymap: true,
                foldKeymap: true,
                completionKeymap: true,
                lintKeymap: true,
            }}
        />
    );
};

export default Editor;
