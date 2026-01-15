import React from 'react';
import { Home, FileText, Settings, FolderOpen } from 'lucide-react';
import { useStore } from '../store/useStore';
import clsx from 'clsx';

const Sidebar = () => {
    // We can add actions here
    const { toggleSidebar } = useStore();

    return (
        <div className="w-12 bg-gray-950 flex flex-col items-center py-2 gap-4 border-r border-gray-800 shrink-0 z-10">
            <div className="p-2 bg-gray-800 rounded text-blue-400 cursor-pointer" title="Explorer">
                <FolderOpen size={20} />
            </div>
            <div className="p-2 hover:bg-gray-800 rounded text-gray-400 cursor-pointer" title="Search">
                <FileText size={20} />
            </div>
            <div className="mt-auto p-2 hover:bg-gray-800 rounded text-gray-400 cursor-pointer" title="Settings">
                <Settings size={20} />
            </div>
        </div>
    );
};

export default Sidebar;
