import React, { useEffect, useRef } from 'react';

export interface ContextMenuItem {
  label: string;
  icon?: React.ReactNode;
  onClick: () => void;
  disabled?: boolean;
}

interface ContextMenuProps {
  x: number;
  y: number;
  items: ContextMenuItem[];
  onClose: () => void;
}

export const ContextMenu: React.FC<ContextMenuProps> = ({ x, y, items, onClose }) => {
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    const handleScroll = () => onClose();

    document.addEventListener('mousedown', handleClick, true);
    document.addEventListener('keydown', handleKeyDown);
    window.addEventListener('scroll', handleScroll, true);

    return () => {
      document.removeEventListener('mousedown', handleClick, true);
      document.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('scroll', handleScroll, true);
    };
  }, [onClose]);

  // Viewport clamping
  const style: React.CSSProperties = {
    position: 'fixed',
    zIndex: 9999,
  };

  // Calculate position after render for clamping
  useEffect(() => {
    if (!menuRef.current) return;
    const rect = menuRef.current.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    let left = x;
    let top = y;

    if (left + rect.width > vw) left = vw - rect.width - 4;
    if (top + rect.height > vh) top = vh - rect.height - 4;
    if (left < 0) left = 4;
    if (top < 0) top = 4;

    menuRef.current.style.left = `${left}px`;
    menuRef.current.style.top = `${top}px`;
  }, [x, y]);

  return (
    <div
      ref={menuRef}
      style={{ ...style, left: x, top: y }}
      className="bg-white border border-gray-200 shadow-lg rounded py-1 min-w-[160px]"
    >
      {items.map((item, i) => (
        <button
          key={i}
          onClick={() => { item.onClick(); onClose(); }}
          disabled={item.disabled}
          className="w-full flex items-center px-3 py-1.5 text-sm text-gray-700 hover:bg-blue-50 hover:text-blue-700 disabled:opacity-40 disabled:cursor-not-allowed text-left"
        >
          {item.icon && <span className="mr-2 flex-shrink-0">{item.icon}</span>}
          {item.label}
        </button>
      ))}
    </div>
  );
};
