import React, { useEffect, useRef } from 'react';
import { marked } from 'marked';
import DOMPurify from 'dompurify';
import { useTheme } from '../contexts/ThemeContext';
import './MarkdownPreview.css';

function MarkdownPreview({ content }) {
  const { theme } = useTheme();
  const previewRef = useRef(null);
  const tocRef = useRef(null);

  useEffect(() => {
    if (!previewRef.current) return;

    // Configure marked
    marked.setOptions({
      breaks: true,
      gfm: true,
      headerIds: true,
      mangle: false
    });

    // Generate TOC
    const headings = [];
    const renderer = new marked.Renderer();
    const originalHeading = renderer.heading.bind(renderer);
    
    renderer.heading = function(text, level) {
      const id = text.toLowerCase().replace(/[^\w]+/g, '-');
      headings.push({ level, text, id });
      return `<h${level} id="${id}">${text}</h${level}>`;
    };

    // Render markdown
    const html = marked.parse(content, { renderer });
    const sanitized = DOMPurify.sanitize(html);
    previewRef.current.innerHTML = sanitized;

    // Generate TOC
    if (tocRef.current && headings.length > 0) {
      const tocHtml = headings.map(({ level, text, id }) => {
        const indent = (level - 1) * 20;
        return `<div class="toc-item" style="padding-left: ${indent}px;">
          <a href="#${id}" data-id="${id}">${text}</a>
        </div>`;
      }).join('');
      tocRef.current.innerHTML = tocHtml;

      // Add click handlers for TOC links
      const links = tocRef.current.querySelectorAll('a');
      links.forEach(link => {
        link.addEventListener('click', (e) => {
          e.preventDefault();
          const targetId = link.getAttribute('data-id');
          const target = document.getElementById(targetId);
          if (target) {
            target.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }
        });
      });
    } else if (tocRef.current) {
      tocRef.current.innerHTML = '';
    }
  }, [content]);

  return (
    <div className={`markdown-preview ${theme}`}>
      <div ref={tocRef} className={`toc ${theme}`}></div>
      <div ref={previewRef} className="markdown-content"></div>
    </div>
  );
}

export default MarkdownPreview;

