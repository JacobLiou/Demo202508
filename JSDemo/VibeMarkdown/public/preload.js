const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('electronAPI', {
  // File operations
  readFile: (filePath) => ipcRenderer.invoke('read-file', filePath),
  writeFile: (filePath, content) => ipcRenderer.invoke('write-file', filePath, content),
  showSaveDialog: () => ipcRenderer.invoke('show-save-dialog'),
  showOpenDialog: () => ipcRenderer.invoke('show-open-dialog'),
  
  // Window controls
  setAlwaysOnTop: (flag) => ipcRenderer.send('set-always-on-top', flag),
  snapWindow: (position) => ipcRenderer.send('snap-window', position),
  
  // Menu events
  onMenuAction: (callback) => {
    ipcRenderer.on('menu-new-file', callback);
    ipcRenderer.on('menu-open-files', (event, filePaths) => callback({ type: 'open-files', filePaths }));
    ipcRenderer.on('menu-save-file', callback);
    ipcRenderer.on('menu-save-as-file', callback);
    ipcRenderer.on('menu-undo', callback);
    ipcRenderer.on('menu-redo', callback);
    ipcRenderer.on('menu-find', callback);
    ipcRenderer.on('menu-replace', callback);
    ipcRenderer.on('menu-goto-line', callback);
    ipcRenderer.on('toggle-dark-mode', callback);
    ipcRenderer.on('change-language', (event, lang) => callback({ type: 'change-language', lang }));
    ipcRenderer.on('always-on-top-changed', (event, flag) => callback({ type: 'always-on-top-changed', flag }));
    ipcRenderer.on('snap-changed', (event, position) => callback({ type: 'snap-changed', position }));
  },
  
  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel)
});

