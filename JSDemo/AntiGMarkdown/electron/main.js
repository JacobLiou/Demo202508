const { app, BrowserWindow, ipcMain, screen } = require('electron');
const path = require('path');

process.env.DIST = path.join(__dirname, '../dist');
process.env.PUBLIC = app.isPackaged ? process.env.DIST : path.join(__dirname, '../public');

let win;
// 🚧 Use ['ENV_NAME'] avoid vite:define plugin - Vite@2.x
const VITE_DEV_SERVER_URL = process.env['VITE_DEV_SERVER_URL'];

function createWindow() {
    win = new BrowserWindow({
        width: 1200,
        height: 800,
        minWidth: 800,
        minHeight: 600,
        frame: false, // Custom titlebar as requested
        webPreferences: {
            preload: path.join(__dirname, 'preload.js'),
            nodeIntegration: false,
            contextIsolation: true,
        },
    });

    // Test active push message to Renderer-process.
    win.webContents.on('did-finish-load', () => {
        win?.webContents.send('main-process-message', (new Date).toLocaleString());
    });

    if (process.env.NODE_ENV === 'development') {
        win.loadURL('http://localhost:5173');
        win.webContents.openDevTools();
    } else {
        // win.loadFile('dist/index.html')
        win.loadFile(path.join(process.env.DIST, 'index.html'));
    }

    // Window Controls
    ipcMain.on('window:minimize', () => win.minimize());
    ipcMain.on('window:maximize', () => {
        if (win.isMaximized()) win.unmaximize();
        else win.maximize();
    });
    ipcMain.on('window:close', () => win.close());

    // Always on Top
    ipcMain.handle('window:toggle-top', (event, flag) => {
        win.setAlwaysOnTop(flag);
        return win.isAlwaysOnTop();
    });

    // Snapping
    ipcMain.on('window:snap', (event, position) => {
        // position: 'left' | 'right' | 'reset'
        const { width: screenWidth, height: screenHeight } = screen.getPrimaryDisplay().workAreaSize;

        if (position === 'left') {
            win.setBounds({ x: 0, y: 0, width: screenWidth / 2, height: screenHeight });
        } else if (position === 'right') {
            win.setBounds({ x: screenWidth / 2, y: 0, width: screenWidth / 2, height: screenHeight });
        } else {
            // Reset to center default
            const w = 1200, h = 800;
            win.setBounds({
                x: Math.round((screenWidth - w) / 2),
                y: Math.round((screenHeight - h) / 2),
                width: w,
                height: h
            });
        }
    });
}

// Quit when all windows are closed, except on macOS. There, it's common
// for applications and their menu bar to stay active until the user quits
// explicitly with Cmd + Q.
app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

app.on('activate', () => {
    // On OS X it's common to re-create a window in the app when the
    // dock icon is clicked and there are no other windows open.
    if (BrowserWindow.getAllWindows().length === 0) {
        createWindow();
    }
});

app.whenReady().then(createWindow);
