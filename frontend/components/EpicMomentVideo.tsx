import React from "react";

interface EpicMomentVideoProps {
  epicMomentVideoUrls: string[];
}

export const EpicMomentVideo: React.FC<EpicMomentVideoProps> = ({
  epicMomentVideoUrls,
}) => {
  if (!epicMomentVideoUrls || epicMomentVideoUrls.length === 0) return null;

  return (
    <div className="grid gap-6 grid-cols-1 md:grid-cols-2">
      {epicMomentVideoUrls.map((videoUrl, idx) => (
        <div
          key={videoUrl + idx}
          className="bg-black rounded-lg overflow-hidden border-2 border-yellow-600 shadow-2xl shadow-yellow-500/10"
        >
          <h2>Chapter {idx + 1}</h2>
          <video
            src={videoUrl}
            controls
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
