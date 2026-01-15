const { app, BrowserWindow, ipcMain, dialog, Menu } = require('electron');
const path = require('path');
const fs = require('fs').promises;

const isDev = process.env.ELECTRON_IS_DEV === '1' || process.env.NODE_ENV === 'development';

let mainWindow;
let isAlwaysOnTop = false;
let isSnapped = false;
let snapPosition = null;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    minWidth: 800,
    minHeight: 600,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js')
    },
    icon: path.join(__dirname, 'icon.png'),
    titleBarStyle: process.platform === 'darwin' ? 'hiddenInset' : 'default'
  });

  const startUrl = isDev 
    ? 'http://localhost:3000' 
    : `file://${path.join(__dirname, '../build/index.html')}`;
  
  mainWindow.loadURL(startUrl);

  if (isDev) {
    mainWindow.webContents.openDevTools();
  }

  // 创建应用菜单
  createMenu();
}

function createMenu() {
  const template = [
    {
      label: 'File',
      submenu: [
        {
          label: 'New',
          accelerator: 'CmdOrCtrl+N',
          click: () => mainWindow.webContents.send('menu-new-file')
        },
        {
          label: 'Open',
          accelerator: 'CmdOrCtrl+O',
          click: async () => {
            const result = await dialog.showOpenDialog(mainWindow, {
              properties: ['openFile', 'multiSelections'],
              filters: [
                { name: 'Markdown', extensions: ['md', 'markdown'] },
                { name: 'All Files', extensions: ['*'] }
              ]
            });
            if (!result.canceled) {
              mainWindow.webContents.send('menu-open-files', result.filePaths);
            }
          }
        },
        { type: 'separator' },
        {
          label: 'Save',
          accelerator: 'CmdOrCtrl+S',
          click: () => mainWindow.webContents.send('menu-save-file')
        },
        {
          label: 'Save As',
          accelerator: 'CmdOrCtrl+Shift+S',
          click: () => mainWindow.webContents.send('menu-save-as-file')
        },
        { type: 'separator' },
        {
          label: 'Exit',
          accelerator: process.platform === 'darwin' ? 'Cmd+Q' : 'Ctrl+Q',
          click: () => app.quit()
        }
      ]
    },
    {
      label: 'Edit',
      submenu: [
        {
          label: 'Undo',
          accelerator: 'CmdOrCtrl+Z',
          click: () => mainWindow.webContents.send('menu-undo')
        },
        {
          label: 'Redo',
          accelerator: 'CmdOrCtrl+Y',
          click: () => mainWindow.webContents.send('menu-redo')
        },
        { type: 'separator' },
        {
          label: 'Find',
          accelerator: 'CmdOrCtrl+F',
          click: () => mainWindow.webContents.send('menu-find')
        },
        {
          label: 'Replace',
          accelerator: 'CmdOrCtrl+H',
          click: () => mainWindow.webContents.send('menu-replace')
        },
        {
          label: 'Go to Line',
          accelerator: 'CmdOrCtrl+G',
          click: () => mainWindow.webContents.send('menu-goto-line')
        }
      ]
    },
    {
      label: 'View',
      submenu: [
        {
          label: 'Always On Top',
          type: 'checkbox',
          checked: isAlwaysOnTop,
          click: (item) => {
            isAlwaysOnTop = !isAlwaysOnTop;
            mainWindow.setAlwaysOnTop(isAlwaysOnTop);
            item.checked = isAlwaysOnTop;
            mainWindow.webContents.send('always-on-top-changed', isAlwaysOnTop);
          }
        },
        {
          label: 'Snap to Left',
          click: () => {
            snapWindow('left');
          }
        },
        {
          label: 'Snap to Right',
          click: () => {
            snapWindow('right');
          }
        },
        {
          label: 'Unsnap',
          click: () => {
            unsnapWindow();
          }
        },
        { type: 'separator' },
        {
          label: 'Toggle Dark Mode',
          accelerator: 'CmdOrCtrl+Shift+D',
          click: () => mainWindow.webContents.send('toggle-dark-mode')
        },
        { type: 'separator' },
        {
          label: 'Reload',
          accelerator: 'CmdOrCtrl+R',
          click: () => mainWindow.reload()
        },
        {
          label: 'Toggle Developer Tools',
          accelerator: process.platform === 'darwin' ? 'Alt+Cmd+I' : 'Ctrl+Shift+I',
          click: () => mainWindow.webContents.toggleDevTools()
        }
      ]
    },
    {
      label: 'Language',
      submenu: [
        {
          label: 'English',
          click: () => mainWindow.webContents.send('change-language', 'en')
        },
        {
          label: '中文',
          click: () => mainWindow.webContents.send('change-language', 'zh')
        }
      ]
    }
  ];

  const menu = Menu.buildFromTemplate(template);
  Menu.setApplicationMenu(menu);
}

function snapWindow(position) {
  if (!mainWindow) return;
  
  const { screen } = require('electron');
  const display = screen.getPrimaryDisplay();
  const { width, height } = display.workAreaSize;
  const { x, y } = display.workArea;
  
  isSnapped = true;
  snapPosition = position;
  
  if (position === 'left') {
    mainWindow.setBounds({
      x: x,
      y: y,
      width: width / 2,
      height: height
    });
  } else if (position === 'right') {
    mainWindow.setBounds({
      x: x + width / 2,
      y: y,
      width: width / 2,
      height: height
    });
  }
  
  mainWindow.webContents.send('snap-changed', position);
}

function unsnapWindow() {
  if (!mainWindow || !isSnapped) return;
  
  isSnapped = false;
  snapPosition = null;
  
  const { screen } = require('electron');
  const display = screen.getPrimaryDisplay();
  const { width, height } = display.workAreaSize;
  
  mainWindow.setBounds({
    width: 1200,
    height: 800
  });
  
  mainWindow.center();
  mainWindow.webContents.send('snap-changed', null);
}

// IPC handlers
ipcMain.handle('read-file', async (event, filePath) => {
  try {
    const content = await fs.readFile(filePath, 'utf-8');
    return { success: true, content };
  } catch (error) {
    return { success: false, error: error.message };
  }
});

ipcMain.handle('write-file', async (event, filePath, content) => {
  try {
    await fs.writeFile(filePath, content, 'utf-8');
    return { success: true };
  } catch (error) {
    return { success: false, error: error.message };
  }
});

ipcMain.handle('show-save-dialog', async (event) => {
  const result = await dialog.showSaveDialog(mainWindow, {
    filters: [
      { name: 'Markdown', extensions: ['md'] },
      { name: 'All Files', extensions: ['*'] }
    ]
  });
  return result;
});

ipcMain.handle('show-open-dialog', async (event) => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openFile', 'multiSelections'],
    filters: [
      { name: 'Markdown', extensions: ['md', 'markdown'] },
      { name: 'All Files', extensions: ['*'] }
    ]
  });
  return result;
});

ipcMain.on('set-always-on-top', (event, flag) => {
  isAlwaysOnTop = flag;
  mainWindow.setAlwaysOnTop(flag);
});

ipcMain.on('snap-window', (event, position) => {
  if (position) {
    snapWindow(position);
  } else {
    unsnapWindow();
  }
});

app.whenReady().then(() => {
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

