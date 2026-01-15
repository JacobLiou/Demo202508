import React, { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../contexts/ThemeContext';
import './GotoLineDialog.css';

function GotoLineDialog({ onClose, textareaRef }) {
  const { t } = useTranslation();
  const { theme } = useTheme();
  const [lineNumber, setLineNumber] = useState('');
  const inputRef = useRef(null);

  useEffect(() => {
    if (inputRef.current) {
      inputRef.current.focus();
    }
  }, []);

  const handleGo = () => {
    if (!textareaRef.current || !lineNumber) return;

    const lineNum = parseInt(lineNumber, 10);
    if (isNaN(lineNum) || lineNum < 1) {
      return;
    }

    const textarea = textareaRef.current;
    const text = textarea.value;
    const lines = text.split('\n');
    
    if (lineNum > lines.length) {
      return;
    }

    // Calculate position
    let position = 0;
    for (let i = 0; i < lineNum - 1; i++) {
      position += lines[i].length + 1; // +1 for newline
    }

    textarea.setSelectionRange(position, position);
    textarea.focus();
    
    // Scroll to position
    const lineHeight = 20; // Approximate line height
    const scrollTop = (lineNum - 1) * lineHeight;
    textarea.scrollTop = scrollTop;

    onClose();
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Escape') {
      onClose();
    } else if (e.key === 'Enter') {
      e.preventDefault();
      handleGo();
    }
  };

  return (
    <div className={`goto-line-dialog-overlay ${theme}`} onClick={onClose}>
      <div className={`goto-line-dialog ${theme}`} onClick={(e) => e.stopPropagation()}>
        <div className="goto-line-header">
          <span>{t('edit.gotoLine')}</span>
          <button className="close-btn" onClick={onClose}>×</button>
        </div>
        
        <div className="goto-line-content">
          <div className="input-group">
            <label>{t('edit.gotoLine')}:</label>
            <input
              ref={inputRef}
              type="number"
              value={lineNumber}
              onChange={(e) => setLineNumber(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={t('editor.gotoLinePlaceholder')}
              min="1"
            />
          </div>
          
          <div className="button-group">
            <button onClick={handleGo} disabled={!lineNumber}>
              Go
            </button>
            <button onClick={onClose}>Cancel</button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default GotoLineDialog;

