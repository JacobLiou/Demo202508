import React, { useState } from 'react';
import Split from 'react-split';
import { Home, FileText, Settings, X, Minus, Square, Copy } from 'lucide-react';

function App() {
    const [activeTab, setActiveTab] = useState('welcome.md');

    return (
        <div className="h-screen w-screen flex flex-col bg-gray-900 text-gray-200 overflow-hidden font-sans">
            {/* Custom Titlebar */}
            <div className="h-8 bg-gray-950 flex items-center justify-between px-2 select-none drag-region">
                <div className="flex items-center gap-2 text-xs font-medium text-gray-400">
                    <FileText size={14} />
                    <span>Markdown Editor</span>
                </div>
                <div className="flex items-center gap-1 no-drag">
                    <button className="p-1 hover:bg-gray-800 rounded" onClick={() => window.electronAPI?.minimize()}>
                        <Minus size={14} />
                    </button>
                    <button className="p-1 hover:bg-gray-800 rounded" onClick={() => window.electronAPI?.maximize()}>
                        <Square size={12} />
                    </button>
                    <button className="p-1 hover:bg-red-900 rounded" onClick={() => window.electronAPI?.close()}>
                        <X size={14} />
                    </button>
                </div>
            </div>

            {/* Main Content */}
            <div className="flex-1 flex overflow-hidden">
                {/* Sidebar */}
                <div className="w-12 bg-gray-950 flex flex-col items-center py-2 gap-4 border-r border-gray-800">
                    <div className="p-2 bg-gray-800 rounded text-blue-400 cursor-pointer">
                        <Home size={20} />
                    </div>
                    <div className="p-2 hover:bg-gray-800 rounded text-gray-400 cursor-pointer">
                        <FileText size={20} />
                    </div>
                    <div className="mt-auto p-2 hover:bg-gray-800 rounded text-gray-400 cursor-pointer">
                        <Settings size={20} />
                    </div>
                </div>

                {/* Editor Area */}
                <div className="flex-1 flex flex-col min-w-0">
                    {/* Tab Bar */}
                    <div className="h-9 bg-gray-900 flex items-center border-b border-gray-800 overflow-x-auto">
                        <div className={`
              h-full px-4 flex items-center gap-2 text-sm border-r border-gray-800 cursor-pointer
              ${activeTab === 'welcome.md' ? 'bg-gray-800 text-white' : 'text-gray-400 hover:bg-gray-800'}
            `}>
                            <span>welcome.md</span>
                            <X size={12} className="hover:text-white" />
                        </div>
                        <div className={`
              h-full px-4 flex items-center gap-2 text-sm border-r border-gray-800 cursor-pointer text-gray-400 hover:bg-gray-800
            `}>
                            <span>notes.md</span>
                            <X size={12} className="hover:text-white" />
                        </div>
                    </div>

                    {/* Editor/Preview Split */}
                    <div className="flex-1 relative">
                        <Split
                            className="flex h-full"
                            sizes={[50, 50]}
                            minSize={100}
                            expandToMin={false}
                            gutterSize={4}
                            gutterAlign="center"
                            snapOffset={30}
                            dragInterval={1}
                            direction="horizontal"
                            cursor="col-resize"
                        >
                            <div className="bg-[#1e1e1e] h-full p-4 overflow-auto">
                                <p className="text-gray-500 font-mono text-sm">Editor Placeholder...</p>
                            </div>
                            <div className="bg-[#1e1e1e] h-full border-l border-gray-800 p-8 overflow-auto prose prose-invert max-w-none">
                                <h1>Welcome to Markdown Editor</h1>
                                <p>This is the preview area.</p>
                            </div>
                        </Split>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default App;
