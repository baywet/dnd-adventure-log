
import React from 'react';
import { SpinnerIcon } from './icons/SpinnerIcon';

interface ProcessingViewProps {
  message: string;
}

export const ProcessingView: React.FC<ProcessingViewProps> = ({ message }) => {
  return (
    <div className="text-center p-8 bg-gray-800 border border-yellow-600/50 rounded-lg flex flex-col items-center justify-center min-h-[100px]">
      <SpinnerIcon className="w-16 h-16 text-yellow-400 mb-6" />
      <h2 className="text-2xl font-title text-yellow-400 mb-2">The Ritual is Underway...</h2>
      <p className="text-lg text-gray-300 animate-pulse">{message}</p>
    </div>
  );
};
