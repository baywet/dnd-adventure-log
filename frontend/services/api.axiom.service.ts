import axios from 'axios';

export const API_BASE_URL = 'http://localhost:5051';

export class ApiAxiomService {
  // --- Campaigns ---
  static async listCampaigns(): Promise<any[]> {
    const response = await axios.get(`${API_BASE_URL}/campaigns`);
    return response.data;
  }

  static async createCampaign(campaignName: string): Promise<any> {
    const response = await axios.post(`${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}`);
    return response.data;
  }

  static async deleteCampaign(campaignName: string): Promise<any> {
    const response = await axios.delete(`${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}`);
    return response.data;
  }

  // --- Recordings ---
  static async uploadRecording(campaignName: string, files: File[]): Promise<any[]> {
    const formData = new FormData();
    files.forEach(file => formData.append('files', file));
    const response = await axios.post(
      `${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}/recordings`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    );
    return response.data;
  }

  static async listRecordings(campaignName: string): Promise<any[]> {
    const response = await axios.get(`${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}/recordings`);
    return response.data;
  }

  static async createEpicMoment(campaignName: string, recordingName: string, data?: any): Promise<any> {
    const response = await axios.post(
      `${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}/recordings/${encodeURIComponent(recordingName)}/epic-moment`,
      data
    );
    return response.data;
  }

  static async getEpicMoment(campaignName: string, recordingName: string): Promise<any> {
    const response = await axios.get(
      `${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}/recordings/${encodeURIComponent(recordingName)}/epic-moment`
    );
    return response.data;
  }

  // --- Characters ---
  static async generateCharacters(campaignName: string): Promise<any> {
    const response = await axios.post(
      `${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}/characters`
    );
    return response.data;
  }

  static async listCharacters(campaignName: string): Promise<any[]> {
    const response = await axios.get(
      `${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}/characters`
    );
    return response.data;
  }

  static async generateCharacterProfile(campaignName: string, characterName: string): Promise<any> {
    const response = await axios.post(
      `${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}/characters/profile/${encodeURIComponent(characterName)}`
    );
    return response.data;
  }

  static async getCharacterProfile(campaignName: string, characterName: string): Promise<any> {
    const response = await axios.get(
      `${API_BASE_URL}/campaigns/${encodeURIComponent(campaignName)}/characters/profile/${encodeURIComponent(characterName)}`
    );
    return response.data;
  }
}