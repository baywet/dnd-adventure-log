import React from 'react';
import { TrashIcon } from './icons/TrashIcon';
import { ApiService } from '../services/api.service';

interface UploadedFilesProps {
  files: File[];
  onDelete: (index: number) => void;
}

export const UploadedFiles: React.FC<UploadedFilesProps> = ({ files, onDelete }) => {
  if (files.length === 0) {
    return null;
  }

  const handleDelete = async (index: number) => {
    try {
      await ApiService.deleteRecording(files[index].name);
      onDelete(index);
    } catch (error) {
      alert('Failed to delete file.');
    }
  };

  return (
    <section>
      <h2 className="text-4xl font-title text-center text-yellow-400 mb-8">Uploaded Audio Logs</h2>
      <div className="bg-gray-800 p-4 rounded-lg border border-gray-700 max-w-2xl mx-auto">
        <ul className="space-y-2">
          {files.map((file, index) => (
            <li key={`${file.name}-${index}`} className="flex justify-between items-center bg-gray-700 p-3 rounded-md hover:bg-gray-600/50">
              <span className="text-gray-300 truncate" title={file.name}>
                {file.name}
              </span>
              <button
                onClick={() => handleDelete(index)}
                className="text-gray-500 hover:text-red-400 transition-colors duration-200 p-1 rounded-full hover:bg-gray-900/50"
                aria-label={`Delete ${file.name}`}
              >
                <TrashIcon className="w-5 h-5" />
              </button>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
};
