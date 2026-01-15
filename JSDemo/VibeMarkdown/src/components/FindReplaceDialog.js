import React, { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../contexts/ThemeContext';
import './FindReplaceDialog.css';

function FindReplaceDialog({ mode, onClose, textareaRef, content, onReplace }) {
  const { t } = useTranslation();
  const { theme } = useTheme();
  const [findText, setFindText] = useState('');
  const [replaceText, setReplaceText] = useState('');
  const [matchCase, setMatchCase] = useState(false);
  const [matchCount, setMatchCount] = useState(0);
  const [currentMatch, setCurrentMatch] = useState(0);
  const findInputRef = useRef(null);

  useEffect(() => {
    if (findInputRef.current) {
      findInputRef.current.focus();
    }
  }, []);

  useEffect(() => {
    if (!findText || !textareaRef.current) {
      setMatchCount(0);
      setCurrentMatch(0);
      return;
    }

    const text = matchCase ? content : content.toLowerCase();
    const searchText = matchCase ? findText : findText.toLowerCase();
    const regex = new RegExp(
      searchText.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'),
      'g'
    );
    const matches = text.match(regex);
    setMatchCount(matches ? matches.length : 0);
    setCurrentMatch(0);
  }, [findText, matchCase, content]);

  const findNext = () => {
    if (!findText || !textareaRef.current) return;

    const textarea = textareaRef.current;
    const text = textarea.value;
    const searchText = matchCase ? findText : findText.toLowerCase();
    const textLower = matchCase ? text : text.toLowerCase();
    
    let startPos = textarea.selectionEnd;
    let index = textLower.indexOf(searchText, startPos);
    
    if (index === -1) {
      // Wrap around
      index = textLower.indexOf(searchText, 0);
    }
    
    if (index !== -1) {
      textarea.setSelectionRange(index, index + findText.length);
      textarea.focus();
      setCurrentMatch(prev => {
        const newPos = prev + 1;
        return newPos > matchCount ? 1 : newPos;
      });
    }
  };

  const findPrevious = () => {
    if (!findText || !textareaRef.current) return;

    const textarea = textareaRef.current;
    const text = textarea.value;
    const searchText = matchCase ? findText : findText.toLowerCase();
    const textLower = matchCase ? text : text.toLowerCase();
    
    let startPos = textarea.selectionStart - 1;
    let index = textLower.lastIndexOf(searchText, startPos);
    
    if (index === -1) {
      // Wrap around
      index = textLower.lastIndexOf(searchText, text.length);
    }
    
    if (index !== -1) {
      textarea.setSelectionRange(index, index + findText.length);
      textarea.focus();
      setCurrentMatch(prev => {
        const newPos = prev - 1;
        return newPos < 1 ? matchCount : newPos;
      });
    }
  };

  const replace = () => {
    if (!findText || !textareaRef.current) return;

    const textarea = textareaRef.current;
    const text = textarea.value;
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selectedText = text.substring(start, end);
    
    const searchText = matchCase ? findText : findText.toLowerCase();
    const selectedLower = matchCase ? selectedText : selectedText.toLowerCase();
    
    if (selectedLower === searchText) {
      const newText = text.substring(0, start) + replaceText + text.substring(end);
      onReplace(newText);
      textarea.value = newText;
      textarea.setSelectionRange(start, start + replaceText.length);
      findNext();
    }
  };

  const replaceAll = () => {
    if (!findText) return;

    const textarea = textareaRef.current;
    const text = textarea.value;
    const searchText = matchCase ? findText : findText.toLowerCase();
    
    let newText = text;
    let textLower = matchCase ? text : text.toLowerCase();
    let index = textLower.indexOf(searchText);
    let count = 0;
    
    while (index !== -1) {
      newText = newText.substring(0, index) + replaceText + newText.substring(index + findText.length);
      textLower = matchCase ? newText : newText.toLowerCase();
      index = textLower.indexOf(searchText, index + replaceText.length);
      count++;
    }
    
    if (count > 0) {
      onReplace(newText);
      textarea.value = newText;
      setMatchCount(0);
      setCurrentMatch(0);
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Escape') {
      onClose();
    } else if (e.key === 'Enter' && e.shiftKey) {
      e.preventDefault();
      findPrevious();
    } else if (e.key === 'Enter') {
      e.preventDefault();
      findNext();
    } else if (e.key === 'F3') {
      e.preventDefault();
      findNext();
    }
  };

  return (
    <div className={`find-replace-dialog-overlay ${theme}`} onClick={onClose}>
      <div className={`find-replace-dialog ${theme}`} onClick={(e) => e.stopPropagation()}>
        <div className="find-replace-header">
          <span>{mode === 'replace' ? t('edit.replace') : t('edit.find')}</span>
          <button className="close-btn" onClick={onClose}>×</button>
        </div>
        
        <div className="find-replace-content">
          <div className="input-group">
            <label>{t('edit.find')}:</label>
            <input
              ref={findInputRef}
              type="text"
              value={findText}
              onChange={(e) => setFindText(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={t('editor.findPlaceholder')}
            />
          </div>
          
          {mode === 'replace' && (
            <div className="input-group">
              <label>{t('edit.replace')}:</label>
              <input
                type="text"
                value={replaceText}
                onChange={(e) => setReplaceText(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder={t('editor.replacePlaceholder')}
              />
            </div>
          )}
          
          <div className="options-group">
            <label>
              <input
                type="checkbox"
                checked={matchCase}
                onChange={(e) => setMatchCase(e.target.checked)}
              />
              Match case
            </label>
          </div>
          
          {matchCount > 0 && (
            <div className="match-info">
              {currentMatch} / {matchCount}
            </div>
          )}
          
          <div className="button-group">
            <button onClick={findPrevious} disabled={!findText || matchCount === 0}>
              ↑ Previous
            </button>
            <button onClick={findNext} disabled={!findText || matchCount === 0}>
              Next ↓
            </button>
            {mode === 'replace' && (
              <>
                <button onClick={replace} disabled={!findText || matchCount === 0}>
                  Replace
                </button>
                <button onClick={replaceAll} disabled={!findText || matchCount === 0}>
                  Replace All
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export default FindReplaceDialog;

