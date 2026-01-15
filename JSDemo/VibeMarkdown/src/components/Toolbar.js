import React from 'react';
import { useTranslation } from 'react-i18next';
import { useFile } from '../contexts/FileContext';
import { useTheme } from '../contexts/ThemeContext';
import './Toolbar.css';

function Toolbar() {
  const { t } = useTranslation();
  const { createNewTab, openFiles, saveTab, saveTabAs, getActiveTab } = useFile();
  const { theme, toggleTheme } = useTheme();
  const [isAlwaysOnTop, setIsAlwaysOnTop] = React.useState(false);
  const [snapPosition, setSnapPosition] = React.useState(null);

  const activeTab = getActiveTab();

  const handleNew = () => {
    createNewTab();
  };

  const handleOpen = async () => {
    if (!window.electronAPI) return;
    const result = await window.electronAPI.showOpenDialog();
    if (!result.canceled) {
      openFiles(result.filePaths);
    }
  };

  const handleSave = async () => {
    if (activeTab) {
      await saveTab(activeTab.id);
    }
  };

  const handleSaveAs = async () => {
    if (activeTab) {
      await saveTabAs(activeTab.id);
    }
  };

  const handleAlwaysOnTop = () => {
    const newValue = !isAlwaysOnTop;
    setIsAlwaysOnTop(newValue);
    if (window.electronAPI) {
      window.electronAPI.setAlwaysOnTop(newValue);
    }
  };

  const handleSnapLeft = () => {
    if (window.electronAPI) {
      window.electronAPI.snapWindow('left');
      setSnapPosition('left');
    }
  };

  const handleSnapRight = () => {
    if (window.electronAPI) {
      window.electronAPI.snapWindow('right');
      setSnapPosition('right');
    }
  };

  const handleUnsnap = () => {
    if (window.electronAPI) {
      window.electronAPI.snapWindow(null);
      setSnapPosition(null);
    }
  };

  React.useEffect(() => {
    if (!window.electronAPI) return;

    const handleMenuAction = (event) => {
      if (event.type === 'always-on-top-changed') {
        setIsAlwaysOnTop(event.flag);
      } else if (event.type === 'snap-changed') {
        setSnapPosition(event.position);
      }
    };

    window.electronAPI.onMenuAction(handleMenuAction);
  }, []);

  return (
    <div className={`toolbar ${theme}`}>
      <div className="toolbar-section">
        <button onClick={handleNew} title={t('file.new')}>
          {t('file.new')}
        </button>
        <button onClick={handleOpen} title={t('file.open')}>
          {t('file.open')}
        </button>
        <button 
          onClick={handleSave} 
          disabled={!activeTab}
          title={t('file.save')}
        >
          {t('file.save')}
        </button>
        <button 
          onClick={handleSaveAs} 
          disabled={!activeTab}
          title={t('file.saveAs')}
        >
          {t('file.saveAs')}
        </button>
      </div>
      
      <div className="toolbar-section">
        <button
          onClick={handleAlwaysOnTop}
          className={isAlwaysOnTop ? 'active' : ''}
          title={t('view.alwaysOnTop')}
        >
          📌 {t('view.alwaysOnTop')}
        </button>
        {snapPosition ? (
          <button onClick={handleUnsnap} title={t('view.unsnap')}>
            {t('view.unsnap')}
          </button>
        ) : (
          <>
            <button onClick={handleSnapLeft} title={t('view.snapLeft')}>
              ⬅ {t('view.snapLeft')}
            </button>
            <button onClick={handleSnapRight} title={t('view.snapRight')}>
              ➡ {t('view.snapRight')}
            </button>
          </>
        )}
        <button onClick={toggleTheme} title={t('view.darkMode')}>
          {theme === 'dark' ? '☀️' : '🌙'} {t('view.darkMode')}
        </button>
      </div>
    </div>
  );
}

export default Toolbar;

