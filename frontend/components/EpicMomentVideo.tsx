import React from 'react';

interface EpicMomentVideoProps {
  videoUrls: string[];
}

export const EpicMomentVideo: React.FC<EpicMomentVideoProps> = ({ videoUrls }) => {
  if (!videoUrls || videoUrls.length === 0) return null;

  return (
    <div className="grid gap-6 grid-cols-1 md:grid-cols-2">
      {videoUrls.map((videoUrl, idx) => (
        <div
          key={videoUrl + idx}
          className="bg-black rounded-lg overflow-hidden border-2 border-yellow-600 shadow-2xl shadow-yellow-500/10"
        >
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
      ))}
    </div>
  );
};
