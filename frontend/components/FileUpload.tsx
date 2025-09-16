
import React, { useState, useCallback } from 'react';

interface FileUploadProps {
  onFilesUpload: (files: File[]) => void;
}

export const FileUpload: React.FC<FileUploadProps> = ({ onFilesUpload }) => {
  const [dragActive, setDragActive] = useState(false);

  const handleDrag = useCallback((e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.type === "dragenter" || e.type === "dragover") {
      setDragActive(true);
    } else if (e.type === "dragleave") {
      setDragActive(false);
    }
  }, []);

  const handleDrop = useCallback((e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      onFilesUpload(Array.from(e.dataTransfer.files));
    }
  }, [onFilesUpload]);
  
  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    e.preventDefault();
    if (e.target.files && e.target.files.length > 0) {
      onFilesUpload(Array.from(e.target.files));
    }
  };


  return (
    <div className="text-center p-8 bg-gray-800 border-2 border-dashed border-gray-600 rounded-xl transition-colors duration-300">
        <h2 className="text-2xl md:text-3xl font-title text-yellow-400 mb-4">Chronicle Your Adventure</h2>
        <p className="text-gray-400 max-w-xl mx-auto mb-8">
            Upload the audio recording(s) of your epic quest. Our scryers will analyze the tale, immortalize your heroes in portraits, and forge a moving picture of your finest hour.
        </p>
        <div 
          onDragEnter={handleDrag} 
          onDragOver={handleDrag} 
          onDragLeave={handleDrag}
          onDrop={handleDrop}
          className={`relative p-10 border-2 border-dashed rounded-lg transition-all duration-300 ${dragActive ? 'border-yellow-400 bg-gray-700' : 'border-gray-500'}`}
        >
            <input 
              type="file" 
              id="file-upload" 
              className="absolute inset-0 w-full h-full opacity-0 cursor-pointer" 
              onChange={handleChange}
              accept="audio/mp3, audio/wav, audio/mpeg"
              multiple
            />
            <label htmlFor="file-upload" className="flex flex-col items-center justify-center cursor-pointer">
              <svg xmlns="http://www.w3.org/2000/svg" className="w-12 h-12 mb-3 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
              </svg>
              <p className="font-semibold text-lg text-gray-300">
                Drag & Drop your audio files here
              </p>
              <p className="text-gray-500 text-sm">or click to browse</p>
               <p className="text-gray-600 text-xs mt-4">MP3, WAV accepted</p>
            </label>
        </div>
    </div>
  );
};
