import React from 'react';
import { useTranslation } from 'react-i18next';
import { useFile } from '../contexts/FileContext';
import { useTheme } from '../contexts/ThemeContext';
import './TabBar.css';

function TabBar() {
  const { t } = useTranslation();
  const { tabs, activeTabId, setActiveTabId, closeTab } = useFile();
  const { theme } = useTheme();

  if (tabs.length === 0) {
    return null;
  }

  return (
    <div className={`tab-bar ${theme}`}>
      {tabs.map(tab => (
        <div
          key={tab.id}
          className={`tab ${tab.id === activeTabId ? 'active' : ''} ${theme}`}
          onClick={() => setActiveTabId(tab.id)}
        >
          <span className="tab-title">
            {tab.title}
            {tab.isModified && <span className="modified-indicator">●</span>}
          </span>
          <button
            className="tab-close"
            onClick={(e) => {
              e.stopPropagation();
              closeTab(tab.id);
            }}
            title={t('file.close')}
          >
            ×
          </button>
        </div>
      ))}
    </div>
  );
}

export default TabBar;

