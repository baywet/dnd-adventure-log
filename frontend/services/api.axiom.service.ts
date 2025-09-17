import axios from 'axios';

const API_BASE_URL = 'http://localhost:5051';

export class ApiAxiomService {
  // Upload one or more recordings (files)
  static async uploadRecording(files: File[]): Promise<any[]> {
    const formData = new FormData();
    files.forEach(file => formData.append('files', file));
    const response = await axios.post(`${API_BASE_URL}/recordings`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    });
    return response.data;
  }

  // List all recordings
  static async listRecordings(): Promise<{ FileName: string; Url: string }[]> {
    const response = await axios.get(`${API_BASE_URL}/recordings`);
    return response.data;
  }

  // Delete a specific recording by file name
  static async deleteRecording(fileName: string): Promise<any> {
    const response = await axios.delete(`${API_BASE_URL}/recordings/${encodeURIComponent(fileName)}`);
    return response.data;
  }

  // Clean all uploaded recordings, transcriptions, and characters
  static async cleanApp(): Promise<void> {
    await axios.delete(`${API_BASE_URL}/clean-app`);
  }

  // List all transcriptions
  static async listTranscriptions(): Promise<{ FileName: string; Url: string }[]> {
    const response = await axios.get(`${API_BASE_URL}/transcriptions`);
    return response.data;
  }

  // Delete a specific transcription by file name
  static async deleteTranscription(fileName: string): Promise<any> {
    const response = await axios.delete(`${API_BASE_URL}/transcriptions/${encodeURIComponent(fileName)}`);
    return response.data;
  }

  // List all characters
  static async listCharacters(): Promise<any[]> {
    const response = await axios.get(`${API_BASE_URL}/characters`);
    return response.data;
  }

  // Delete a specific character by file name
  static async deleteCharacter(fileName: string): Promise<any> {
    const response = await axios.delete(`${API_BASE_URL}/characters/${encodeURIComponent(fileName)}`);
    return response.data;
  }

  // Get player portrait (POST)
  static async getPlayerPortrait(recordingName: string, characterName: string): Promise<string> {
    const response = await axios.post(
      `${API_BASE_URL}/recordings/${encodeURIComponent(recordingName)}/characters/profile/${encodeURIComponent(characterName)}`
    );
    // If API returns { url: string }
    if (response.data && typeof response.data === 'object' && response.data.url) return response.data.url;
    // If API returns the URL directly as string
    if (typeof response.data === 'string') return response.data;
    throw new Error('Invalid portrait response');
  }
}