
import React from 'react';

export const D20Icon: React.FC<React.SVGProps<SVGSVGElement>> = (props) => (
  <svg
    xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.5"
    strokeLinecap="round"
    strokeLinejoin="round"
    {...props}
  >
    <path d="M12 2l10 10-10 10L2 12 12 2z" />
    <path d="M2 12l5 5" />
    <path d="M22 12l-5 5" />
    <path d="M12 22V12" />
    <path d="M12 2v10l-5 5" />
    <path d="M12 2v10l5 5" />
    <path d="M7 17l5-10" />
    <path d="M17 17L12 7" />
  </svg>
);
