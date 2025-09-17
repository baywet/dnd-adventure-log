const API_BASE_URL = 'http://localhost:5051';

export class ApiService {
  // Upload one or more recordings (files)
  static async uploadRecording(files: File[]): Promise<any> {
    const formData = new FormData();
    files.forEach(file => formData.append('files', file));

    const response = await fetch(`${API_BASE_URL}/recordings`, {
      method: 'POST',
      body: formData
    });

    if (!response.ok) {
      throw new Error(await response.text());
    }
    return await response.json();
  }

  // List all recordings
  static async listRecordings(): Promise<{ FileName: string; Url: string }[]> {
    const response = await fetch(`${API_BASE_URL}/recordings`);
    if (!response.ok) {
      throw new Error(await response.text());
    }
    return await response.json();
  }

  // Delete a specific recording by file name
  static async deleteRecording(fileName: string): Promise<any> {
    const response = await fetch(`${API_BASE_URL}/recordings/${encodeURIComponent(fileName)}`, {
      method: 'DELETE'
    });
    if (!response.ok) {
      throw new Error(await response.text());
    }
    return await response.json();
  }

  // Clean all uploaded recordings, transcriptions, and characters
  static async cleanApp(): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/clean-app`, {
      method: 'DELETE'
    });
    if (!response.ok) {
      throw new Error(await response.text());
    }
  }

  // List all transcriptions
  static async listTranscriptions(): Promise<{ FileName: string; Url: string }[]> {
    const response = await fetch(`${API_BASE_URL}/transcriptions`);
    if (!response.ok) {
      throw new Error(await response.text());
    }
    return await response.json();
  }

  // Delete a specific transcription by file name
  static async deleteTranscription(fileName: string): Promise<any> {
    const response = await fetch(`${API_BASE_URL}/transcriptions/${encodeURIComponent(fileName)}`, {
      method: 'DELETE'
    });
    if (!response.ok) {
      throw new Error(await response.text());
    }
    return await response.json();
  }

  // List all characters
  static async listCharacters(): Promise<{ FileName: string; Url: string }[]> {
    const response = await fetch(`${API_BASE_URL}/characters`, {
      method: 'POST'
    });
    if (!response.ok) {
      throw new Error(await response.text());
    }
    return await response.json();
  }
}