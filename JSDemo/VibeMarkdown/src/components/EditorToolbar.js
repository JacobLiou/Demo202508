import React from 'react';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../contexts/ThemeContext';
import './EditorToolbar.css';

function EditorToolbar({
  showPreview,
  onTogglePreview,
  onUndo,
  onRedo,
  canUndo,
  canRedo,
  onFind,
  onReplace,
  onGotoLine,
  onInsertDateTime
}) {
  const { t } = useTranslation();
  const { theme } = useTheme();

  return (
    <div className={`editor-toolbar ${theme}`}>
      <div className="editor-toolbar-section">
        <button
          onClick={onTogglePreview}
          className={showPreview ? 'active' : ''}
          title="Toggle Preview"
        >
          👁️ {showPreview ? 'Hide Preview' : 'Show Preview'}
        </button>
      </div>
      
      <div className="editor-toolbar-section">
        <button
          onClick={onUndo}
          disabled={!canUndo}
          title={t('edit.undo')}
        >
          ↶ {t('edit.undo')}
        </button>
        <button
          onClick={onRedo}
          disabled={!canRedo}
          title={t('edit.redo')}
        >
          ↷ {t('edit.redo')}
        </button>
      </div>

      <div className="editor-toolbar-section">
        <button onClick={onFind} title={t('edit.find')}>
          🔍 {t('edit.find')}
        </button>
        <button onClick={onReplace} title={t('edit.replace')}>
          🔄 {t('edit.replace')}
        </button>
        <button onClick={onGotoLine} title={t('edit.gotoLine')}>
          📍 {t('edit.gotoLine')}
        </button>
        <button onClick={onInsertDateTime} title={t('edit.insertDateTime')}>
          📅 {t('edit.insertDateTime')}
        </button>
      </div>
    </div>
  );
}

export default EditorToolbar;

