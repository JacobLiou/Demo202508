import React, { createContext, useContext, useState, useCallback } from 'react';

const FileContext = createContext();

export function FileProvider({ children }) {
  const [tabs, setTabs] = useState([]);
  const [activeTabId, setActiveTabId] = useState(null);
  const [nextTabId, setNextTabId] = useState(1);

  const createNewTab = useCallback(() => {
    const newTab = {
      id: `tab-${nextTabId}`,
      title: `Untitled-${nextTabId}`,
      content: '',
      filePath: null,
      isModified: false,
      history: { undo: [], redo: [] }
    };
    setTabs(prev => [...prev, newTab]);
    setActiveTabId(newTab.id);
    setNextTabId(prev => prev + 1);
    return newTab.id;
  }, [nextTabId]);

  const openFile = useCallback(async (filePath) => {
    if (!window.electronAPI) return;
    
    const result = await window.electronAPI.readFile(filePath);
    if (result.success) {
      const fileName = filePath.split(/[/\\]/).pop();
      const newTab = {
        id: `tab-${nextTabId}`,
        title: fileName,
        content: result.content,
        filePath: filePath,
        isModified: false,
        history: { undo: [], redo: [] }
      };
      setTabs(prev => [...prev, newTab]);
      setActiveTabId(newTab.id);
      setNextTabId(prev => prev + 1);
      return newTab.id;
    }
    return null;
  }, [nextTabId]);

  const openFiles = useCallback(async (filePaths) => {
    for (const filePath of filePaths) {
      await openFile(filePath);
    }
  }, [openFile]);

  const closeTab = useCallback((tabId) => {
    setTabs(prev => {
      const newTabs = prev.filter(tab => tab.id !== tabId);
      if (activeTabId === tabId && newTabs.length > 0) {
        const currentIndex = prev.findIndex(tab => tab.id === tabId);
        const newActiveIndex = currentIndex > 0 ? currentIndex - 1 : 0;
        setActiveTabId(newTabs[newActiveIndex].id);
      } else if (newTabs.length === 0) {
        setActiveTabId(null);
      }
      return newTabs;
    });
  }, [activeTabId]);

  const updateTabContent = useCallback((tabId, content) => {
    setTabs(prev => prev.map(tab => 
      tab.id === tabId 
        ? { ...tab, content, isModified: true }
        : tab
    ));
  }, []);

  const saveTab = useCallback(async (tabId) => {
    const tab = tabs.find(t => t.id === tabId);
    if (!tab || !window.electronAPI) return false;

    if (tab.filePath) {
      const result = await window.electronAPI.writeFile(tab.filePath, tab.content);
      if (result.success) {
        setTabs(prev => prev.map(t => 
          t.id === tabId ? { ...t, isModified: false } : t
        ));
        return true;
      }
      return false;
    } else {
      // Save as
      return await saveTabAs(tabId);
    }
  }, [tabs]);

  const saveTabAs = useCallback(async (tabId) => {
    const tab = tabs.find(t => t.id === tabId);
    if (!tab || !window.electronAPI) return false;

    const result = await window.electronAPI.showSaveDialog();
    if (!result.canceled && result.filePath) {
      const writeResult = await window.electronAPI.writeFile(result.filePath, tab.content);
      if (writeResult.success) {
        const fileName = result.filePath.split(/[/\\]/).pop();
        setTabs(prev => prev.map(t => 
          t.id === tabId 
            ? { ...t, filePath: result.filePath, title: fileName, isModified: false }
            : t
        ));
        return true;
      }
    }
    return false;
  }, [tabs]);

  const getActiveTab = useCallback(() => {
    return tabs.find(tab => tab.id === activeTabId);
  }, [tabs, activeTabId]);

  // Menu handlers
  React.useEffect(() => {
    if (!window.electronAPI) return;

    const handleMenuAction = (event) => {
      // Handle string events (direct channel names)
      if (typeof event === 'string') {
        if (event === 'menu-new-file') {
          createNewTab();
        } else if (event === 'menu-save-file' && activeTabId) {
          saveTab(activeTabId);
        } else if (event === 'menu-save-as-file' && activeTabId) {
          saveTabAs(activeTabId);
        }
      } 
      // Handle object events
      else if (event && typeof event === 'object') {
        if (event.type === 'open-files') {
          openFiles(event.filePaths);
        } else if (event.type === 'save-file' && activeTabId) {
          saveTab(activeTabId);
        } else if (event.type === 'save-as-file' && activeTabId) {
          saveTabAs(activeTabId);
        }
      }
    };

    window.electronAPI.onMenuAction(handleMenuAction);
    
    return () => {
      // Cleanup will be handled by App.js
    };
  }, [createNewTab, openFiles, saveTab, saveTabAs, activeTabId]);

  return (
    <FileContext.Provider value={{
      tabs,
      activeTabId,
      setActiveTabId,
      createNewTab,
      openFile,
      openFiles,
      closeTab,
      updateTabContent,
      saveTab,
      saveTabAs,
      getActiveTab
    }}>
      {children}
    </FileContext.Provider>
  );
}

export function useFile() {
  const context = useContext(FileContext);
  if (!context) {
    throw new Error('useFile must be used within FileProvider');
  }
  return context;
}

