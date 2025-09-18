import React, { useEffect, useState } from 'react';
import { ApiAxiomService } from '../services/api.axiom.service';
import { Campaign } from '@/types';

interface CampaignListProps {
  onSelect?: (campaign: Campaign | null) => void;
}

export const CampaignList: React.FC<CampaignListProps> = ({ onSelect }) => {
  const [campaigns, setCampaigns] = useState<Campaign[]>([]);
  const [newCampaign, setNewCampaign] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<Campaign | null>(null);

  const fetchCampaigns = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await ApiAxiomService.listCampaigns();
      // data is an array of strings, map to { name: string }
      setCampaigns(Array.isArray(data) ? data.map((name: string) => ({ name })) : []);
    } catch (err) {
      setError('Failed to load campaigns.');
    } finally {
      setLoading(false);
    }
  };


  useEffect(() => {
    fetchCampaigns();
  }, []);

  useEffect(() => {
    if (onSelect) onSelect(selected);
  }, [selected, onSelect]);

  const handleAdd = async () => {
    if (!newCampaign.trim()) return;
    setLoading(true);
    setError(null);
    try {
      await ApiAxiomService.createCampaign(newCampaign.trim());
      setNewCampaign('');
      fetchCampaigns();
    } catch (err) {
      setError('Failed to add campaign.');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (name: string) => {
    setLoading(true);
    setError(null);
    try {
      await ApiAxiomService.deleteCampaign(name);
      fetchCampaigns();
    } catch (err) {
      setError('Failed to delete campaign.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <section className="bg-gray-800 p-6 rounded-lg border border-gray-700 max-w-xl mx-auto my-8">
      <h2 className="text-2xl font-title text-yellow-400 mb-4">Campaigns</h2>
      <div className="flex mb-4">
        <input
          type="text"
          value={newCampaign}
          onChange={e => setNewCampaign(e.target.value)}
          placeholder="New campaign name"
          className="flex-1 p-2 rounded-l bg-gray-700 text-white border border-gray-600 focus:outline-none"
        />
        <button
          onClick={handleAdd}
          className="bg-yellow-500 hover:bg-yellow-600 text-gray-900 font-bold px-4 py-2 rounded-r"
          disabled={loading}
        >
          Add
        </button>
      </div>
      {error && <div className="text-red-400 mb-2">{error}</div>}
      {loading && <div className="text-gray-400 mb-2">Loading...</div>}
      <ul className="divide-y divide-gray-700">
        {campaigns.map((c) => (
          <li
            key={c.name}
            className={`flex justify-between items-center py-2 ${selected?.name === c.name ? 'bg-yellow-900/30' : ''}`}
          >
            <button
              className={`flex-1 text-left text-white ${selected?.name === c.name ? 'font-bold text-yellow-300' : ''}`}
              onClick={() => setSelected(c)}
              disabled={loading}
            >
              {c.name}
            </button>
            <button
              onClick={() => handleDelete(c.name)}
              className="bg-red-500 hover:bg-red-600 text-white px-3 py-1 rounded ml-2"
              disabled={loading}
            >
              Delete
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
};
