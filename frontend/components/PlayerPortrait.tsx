import React, { useEffect, useState } from 'react';
import type { Player } from '../types';
import { ApiService } from '../services/api.service';
import { API_BASE_URL, ApiAxiomService } from '@/services/api.axiom.service';

interface PlayerPortraitProps {
  player: Player;
}

export const PlayerPortrait: React.FC<PlayerPortraitProps> = ({campaign, player }) => {

  return (
    <div className="bg-gray-800 rounded-lg overflow-hidden border border-gray-700 shadow-lg hover:shadow-yellow-400/20 hover:border-yellow-500/50 transition-all duration-300 transform hover:-translate-y-1">
        <img src={`${API_BASE_URL}/campaigns/${campaign}/characters/profile/${player.name}`} alt={`Portrait of ${player.name}`} className="w-full h-auto aspect-[3/4] object-cover bg-gray-700" />
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
