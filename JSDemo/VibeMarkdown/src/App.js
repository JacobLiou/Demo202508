import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import './App.css';
import Editor from './components/Editor';
import TabBar from './components/TabBar';
import Toolbar from './components/Toolbar';
import { ThemeProvider, useTheme } from './contexts/ThemeContext';
import { FileProvider } from './contexts/FileContext';

function AppContent() {
  const { theme } = useTheme();
  const { i18n } = useTranslation();

  useEffect(() => {
    // Listen to menu actions from Electron
    if (window.electronAPI) {
      const handleMenuAction = (event) => {
        if (event.type === 'change-language') {
          i18n.changeLanguage(event.lang);
        } else if (event.type === 'toggle-dark-mode') {
          // This will be handled by ThemeContext
        }
      };

      window.electronAPI.onMenuAction(handleMenuAction);

      return () => {
        window.electronAPI.removeAllListeners('menu-new-file');
        window.electronAPI.removeAllListeners('menu-open-files');
        window.electronAPI.removeAllListeners('menu-save-file');
        window.electronAPI.removeAllListeners('menu-save-as-file');
        window.electronAPI.removeAllListeners('menu-undo');
        window.electronAPI.removeAllListeners('menu-redo');
        window.electronAPI.removeAllListeners('menu-find');
        window.electronAPI.removeAllListeners('menu-replace');
        window.electronAPI.removeAllListeners('menu-goto-line');
        window.electronAPI.removeAllListeners('toggle-dark-mode');
        window.electronAPI.removeAllListeners('change-language');
      };
    }
  }, [i18n]);

  return (
    <div className={`app ${theme}`}>
      <FileProvider>
        <Toolbar />
        <TabBar />
        <Editor />
      </FileProvider>
    </div>
  );
}

function App() {
  return (
    <ThemeProvider>
      <AppContent />
    </ThemeProvider>
  );
}

export default App;

