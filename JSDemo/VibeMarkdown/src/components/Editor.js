import React, { useState, useRef, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useFile } from '../contexts/FileContext';
import { useTheme } from '../contexts/ThemeContext';
import SplitPane from 'react-split-pane';
import MarkdownPreview from './MarkdownPreview';
import EditorToolbar from './EditorToolbar';
import FindReplaceDialog from './FindReplaceDialog';
import GotoLineDialog from './GotoLineDialog';
import './Editor.css';

function Editor() {
  const { t } = useTranslation();
  const { activeTabId, getActiveTab, updateTabContent } = useFile();
  const { theme } = useTheme();
  const textareaRef = useRef(null);
  const [splitPosition, setSplitPosition] = useState(50);
  const [showPreview, setShowPreview] = useState(true);
  const [showFindReplace, setShowFindReplace] = useState(false);
  const [showGotoLine, setShowGotoLine] = useState(false);
  const [findReplaceMode, setFindReplaceMode] = useState('find'); // 'find' or 'replace'
  const [undoStack, setUndoStack] = useState([]);
  const [redoStack, setRedoStack] = useState([]);
  const [lastContent, setLastContent] = useState('');

  const activeTab = getActiveTab();
  const content = activeTab?.content || '';

  useEffect(() => {
    if (activeTab) {
      setLastContent(activeTab.content);
      if (textareaRef.current) {
        textareaRef.current.value = activeTab.content;
      }
    }
  }, [activeTab?.id]);

  const handleContentChange = (e) => {
    const newContent = e.target.value;
    if (activeTabId) {
      // Save to undo stack
      if (lastContent !== newContent) {
        setUndoStack(prev => [...prev, lastContent]);
        setRedoStack([]);
        setLastContent(newContent);
      }
      updateTabContent(activeTabId, newContent);
    }
  };

  const handleUndo = () => {
    if (undoStack.length > 0 && activeTabId) {
      const previousContent = undoStack[undoStack.length - 1];
      setRedoStack(prev => [lastContent, ...prev]);
      setUndoStack(prev => prev.slice(0, -1));
      setLastContent(previousContent);
      updateTabContent(activeTabId, previousContent);
      if (textareaRef.current) {
        textareaRef.current.value = previousContent;
      }
    }
  };

  const handleRedo = () => {
    if (redoStack.length > 0 && activeTabId) {
      const nextContent = redoStack[0];
      setUndoStack(prev => [...prev, lastContent]);
      setRedoStack(prev => prev.slice(1));
      setLastContent(nextContent);
      updateTabContent(activeTabId, nextContent);
      if (textareaRef.current) {
        textareaRef.current.value = nextContent;
      }
    }
  };

  const handleInsertDateTime = () => {
    if (!activeTabId || !textareaRef.current) return;
    
    const now = new Date();
    const dateTimeStr = now.toLocaleString();
    const textarea = textareaRef.current;
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const currentContent = content;
    const newContent = 
      currentContent.substring(0, start) + 
      dateTimeStr + 
      currentContent.substring(end);
    
    updateTabContent(activeTabId, newContent);
    textarea.value = newContent;
    textarea.setSelectionRange(start + dateTimeStr.length, start + dateTimeStr.length);
    textarea.focus();
  };

  // Menu handlers
  useEffect(() => {
    if (!window.electronAPI) return;

    const handleMenuAction = (event) => {
      if (event.type === 'undo') {
        handleUndo();
      } else if (event.type === 'redo') {
        handleRedo();
      } else if (event.type === 'find') {
        setFindReplaceMode('find');
        setShowFindReplace(true);
      } else if (event.type === 'replace') {
        setFindReplaceMode('replace');
        setShowFindReplace(true);
      } else if (event.type === 'goto-line') {
        setShowGotoLine(true);
      }
    };

    window.electronAPI.onMenuAction(handleMenuAction);
  }, [activeTabId, undoStack, redoStack, lastContent, content]);

  if (!activeTab) {
    return (
      <div className={`editor-empty ${theme}`}>
        <p>{t('editor.untitled')}</p>
        <p style={{ fontSize: '14px', opacity: 0.7, marginTop: '8px' }}>
          {t('file.new')} {t('file.open')}
        </p>
      </div>
    );
  }

  return (
    <div className={`editor-container ${theme}`}>
      <EditorToolbar
        showPreview={showPreview}
        onTogglePreview={() => setShowPreview(!showPreview)}
        onUndo={handleUndo}
        onRedo={handleRedo}
        canUndo={undoStack.length > 0}
        canRedo={redoStack.length > 0}
        onFind={() => {
          setFindReplaceMode('find');
          setShowFindReplace(true);
        }}
        onReplace={() => {
          setFindReplaceMode('replace');
          setShowFindReplace(true);
        }}
        onGotoLine={() => setShowGotoLine(true)}
        onInsertDateTime={handleInsertDateTime}
      />
      
      {showPreview ? (
        <SplitPane
          split="vertical"
          minSize={200}
          defaultSize={`${splitPosition}%`}
          onChange={setSplitPosition}
          paneStyle={{ overflow: 'auto' }}
        >
          <div className="editor-pane">
            <textarea
              ref={textareaRef}
              className={`editor-textarea ${theme}`}
              value={content}
              onChange={handleContentChange}
              placeholder={t('editor.untitled')}
              spellCheck={false}
            />
          </div>
          <div className="preview-pane">
            <MarkdownPreview content={content} />
          </div>
        </SplitPane>
      ) : (
        <div className="editor-pane-full">
          <textarea
            ref={textareaRef}
            className={`editor-textarea ${theme}`}
            value={content}
            onChange={handleContentChange}
            placeholder={t('editor.untitled')}
            spellCheck={false}
          />
        </div>
      )}

      {showFindReplace && (
        <FindReplaceDialog
          mode={findReplaceMode}
          onClose={() => setShowFindReplace(false)}
          textareaRef={textareaRef}
          content={content}
          onReplace={(newContent) => {
            if (activeTabId) {
              updateTabContent(activeTabId, newContent);
              if (textareaRef.current) {
                textareaRef.current.value = newContent;
              }
            }
          }}
        />
      )}

      {showGotoLine && (
        <GotoLineDialog
          onClose={() => setShowGotoLine(false)}
          textareaRef={textareaRef}
        />
      )}
    </div>
  );
}

export default Editor;

