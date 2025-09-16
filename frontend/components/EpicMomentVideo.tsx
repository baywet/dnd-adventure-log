
import React from 'react';

interface EpicMomentVideoProps {
  videoUrl: string;
}

export const EpicMomentVideo: React.FC<EpicMomentVideoProps> = ({ videoUrl }) => {
  return (
    <div className="bg-black rounded-lg overflow-hidden border-2 border-yellow-600 shadow-2xl shadow-yellow-500/10">
      <video
        src={videoUrl}
        controls
        autoPlay
        loop
        muted
        className="w-full h-full object-contain"
      >
        Your browser does not support the video tag.
      </video>
    </div>
  );
};
