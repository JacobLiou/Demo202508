import React from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeHighlight from 'rehype-highlight';
import 'highlight.js/styles/github-dark.css'; // Ensure this works, otherwise might need to adjust path or install highlight.js explicitly

const Preview = ({ content }) => {
    return (
        <div className="h-full w-full overflow-auto p-8 prose prose-invert max-w-none bg-[#1e1e1e]">
            <ReactMarkdown
                remarkPlugins={[remarkGfm]}
                rehypePlugins={[rehypeHighlight]}
            >
                {content || '*No content*'}
            </ReactMarkdown>
        </div>
    );
};

export default Preview;
