import React from 'react';
import { D20Icon } from './icons/D20Icon';
import { ApiService } from '../services/api.service';

export const Header: React.FC = () => {
  const handleReset = async () => {
    try {
      await ApiService.cleanApp();
      window.location.reload();
    } catch (error) {
      alert('Failed to reset app.');
    }
  };

  return (
    <header className="bg-gray-800/50 backdrop-blur-sm border-b border-yellow-600/30 sticky top-0 z-10">
      <div className="container mx-auto px-4 py-4">
        <div className="flex items-center justify-center space-x-4">
          <D20Icon className="w-10 h-10 text-yellow-400"/>
          <h1 className="text-3xl md:text-4xl font-title text-center text-transparent bg-clip-text bg-gradient-to-r from-yellow-300 to-yellow-500">
            D&D Quest Visualizer
          </h1>
        </div>
        <div className="absolute right-4 top-4">
          <button
            onClick={handleReset}
            className="bg-red-500 hover:bg-red-600 text-white font-bold py-2 px-4 rounded">
            Reset
          </button>
        </div>
      </div>
    </header>
  );
};
