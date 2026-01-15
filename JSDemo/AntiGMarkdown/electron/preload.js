const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('electronAPI', {
    minimize: () => ipcRenderer.send('window:minimize'),
    maximize: () => ipcRenderer.send('window:maximize'),
    close: () => ipcRenderer.send('window:close'),

    toggleAlwaysOnTop: (flag) => ipcRenderer.invoke('window:toggle-top', flag),
    snapWindow: (position) => ipcRenderer.send('window:snap', position),

    // We will add file system hooks here later
});
