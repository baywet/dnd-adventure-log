import React, { useEffect, useState } from 'react';
import type { Player } from '../types';
import { ApiService } from '../services/api.service';

interface PlayerPortraitProps {
  player: Player;
}

export const PlayerPortrait: React.FC<PlayerPortraitProps> = ({ player }) => {
  const [portraitUrl, setPortraitUrl] = useState<string | undefined>(player.portraitUrl);

  useEffect(() => {
    let isMounted = true;
    // If portraitUrl is not present, fetch it from the API
    if (!player.portraitUrl) {
      ApiService.getPlayerPortrait("3-7 - 2_22_18, 1.41 PM", player.name)
        .then((url: string) => {
          if (isMounted) setPortraitUrl(url);
        })
        .catch(() => {
          if (isMounted) setPortraitUrl(undefined);
        });
    } else {
      setPortraitUrl(player.portraitUrl);
    }
    return () => { isMounted = false; };
  }, [player]);

  return (
    <div className="bg-gray-800 rounded-lg overflow-hidden border border-gray-700 shadow-lg hover:shadow-yellow-400/20 hover:border-yellow-500/50 transition-all duration-300 transform hover:-translate-y-1">
      {portraitUrl ? (
        <img src={portraitUrl} alt={`Portrait of ${player.name}`} className="w-full h-auto aspect-[3/4] object-cover bg-gray-700" />
      ) : (
        <div className="w-full aspect-[3/4] bg-gray-700 flex items-center justify-center">
            <p className="text-gray-500">Portrait not found...</p>
        </div>
      )}
      <div className="p-4">
         <h3 className="text-2xl font-title text-yellow-400">{player.name}</h3>
        {player.race && (
          <h3 className="text-lg font-title text-yellow-300">{player.race}</h3>
        )}
        {player.level !== undefined && player.level !== null && (
          <h3 className="text-lg font-title text-yellow-200">Level {player.level}</h3>
        )}
        <p className="text-sm text-gray-400 mt-1">{player.description}</p>
      </div>
    </div>
  );
};
