
import React from 'react';
import type { Player } from '../types';
import { PlayerPortrait } from './PlayerPortrait';
import { EpicMomentVideo } from './EpicMomentVideo';
import { UploadedFiles } from './UploadedFiles';

interface ResultsViewProps {
  files: File[];
  onDeleteFile: (index: number) => void;
  transcript: string;
  players: Player[];
  epicMomentVideoUrls: string[];
  onReset: () => void;
}

export const ResultsView: React.FC<ResultsViewProps> = ({ campaign, files, onDeleteFile, transcript, players, epicMomentVideoUrls, onReset }) => {
  return (
    <div className="space-y-12">
      
      {/* Heroes Gallery */}
      <section>
        <h2 className="text-4xl font-title text-center text-yellow-400 mb-8">The Heroes of the Quest</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-8">
          {players.map((player) => (
            <PlayerPortrait campaign={campaign} key={player.name} player={player} />
          ))}
        </div>
      </section>

      {/* Epic Moment */}
      {epicMomentVideoUrls && (
        <section>
          <h2 className="text-4xl font-title text-center text-yellow-400 mb-8">The Epic Climax</h2>
          <EpicMomentVideo videoUrl={epicMomentVideoUrls} />
        </section>
      )}

      {/* Transcript
      <section>
        <h2 className="text-4xl font-title text-center text-yellow-400 mb-8">The Chronicled Tale</h2>
        <div className="bg-gray-800 p-6 rounded-lg border border-gray-700 max-h-96 overflow-y-auto">
          <p className="whitespace-pre-wrap leading-relaxed text-gray-300">{transcript}</p>
        </div>
      </section> */}
      
       {/* Uploaded Files */}
      {/* <UploadedFiles files={files} onDelete={onDeleteFile} /> */}

      {/* Reset Button */}
      <div className="text-center pt-8">
        <button
          onClick={onReset}
          className="bg-yellow-600 text-gray-900 font-bold py-3 px-8 rounded-lg hover:bg-yellow-500 transition-colors duration-300 text-lg"
        >
          Analyze Another Adventure
        </button>
      </div>

    </div>
  );
};
