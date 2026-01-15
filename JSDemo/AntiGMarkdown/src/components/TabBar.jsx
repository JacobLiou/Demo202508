import React from 'react';
import { X, Plus } from 'lucide-react';
import { useStore } from '../store/useStore';
import clsx from 'clsx';

const TabBar = () => {
    const { files, activeFileId, setActiveFile, closeFile, addFile } = useStore();

    const handleAddNew = () => {
        const newId = Date.now().toString();
        addFile({
            id: newId,
            name: 'Untitled.md',
            content: '',
            path: null,
            isDirty: false
        });
    };

    return (
        <div className="h-9 bg-gray-900 flex items-center border-b border-gray-800 overflow-x-auto no-scrollbar">
            {files.map((file) => (
                <div
                    key={file.id}
                    onClick={() => setActiveFile(file.id)}
                    className={clsx(
                        "h-full px-4 flex items-center gap-2 text-sm border-r border-gray-800 cursor-pointer select-none min-w-[120px] max-w-[200px] group",
                        file.id === activeFileId ? "bg-gray-800 text-white" : "text-gray-400 hover:bg-gray-800"
                    )}
                >
                    <span className="truncate flex-1">{file.name}{file.isDirty ? '*' : ''}</span>
                    <div
                        className="p-0.5 rounded-sm hover:bg-gray-700 opacity-0 group-hover:opacity-100 transition-opacity"
                        onClick={(e) => { e.stopPropagation(); closeFile(file.id); }}
                    >
                        <X size={12} />
                    </div>
                </div>
            ))}
            <div
                className="h-full w-9 flex items-center justify-center text-gray-500 hover:text-white hover:bg-gray-800 cursor-pointer"
                onClick={handleAddNew}
            >
                <Plus size={16} />
            </div>
        </div>
    );
};

export default TabBar;
