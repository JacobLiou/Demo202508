import { create } from 'zustand';

export const useStore = create((set, get) => ({
    files: [
        { id: '1', name: 'welcome.md', content: '# Welcome to Markdown Editor\n\nStart typing...', path: null, isDirty: false }
    ],
    activeFileId: '1',
    sidebarExpanded: true,

    addFile: (file) => set((state) => {
        // Check if file acts like a singleton or exists
        const exists = state.files.find(f => f.path && f.path === file.path);
        if (exists) return { activeFileId: exists.id };
        return { files: [...state.files, file], activeFileId: file.id };
    }),

    closeFile: (id) => set((state) => {
        const newFiles = state.files.filter(f => f.id !== id);
        let newActiveId = state.activeFileId;

        // If closing active file, switch to another
        if (state.activeFileId === id) {
            newActiveId = newFiles.length > 0 ? newFiles[newFiles.length - 1].id : null;
        }

        return { files: newFiles, activeFileId: newActiveId };
    }),

    setActiveFile: (id) => set({ activeFileId: id }),

    updateFileContent: (id, content) => set((state) => ({
        files: state.files.map(f => f.id === id ? { ...f, content, isDirty: true } : f)
    })),

    toggleSidebar: () => set((state) => ({ sidebarExpanded: !state.sidebarExpanded })),

    getActiveFile: () => {
        const state = get();
        return state.files.find(f => f.id === state.activeFileId);
    }
}));
