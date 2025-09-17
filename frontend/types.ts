export interface Player {
  name: string;
  description: string;
  level?: number | null;
  race?: string | null;
  portraitUrl?: string;
  fileUrl?: string;
}

export type Players = Player[];

export enum ProcessState {
  Idle,
  Processing,
  Success,
  Error,
}

export interface Transcription {
  file: string;
  transcriptionFile: string;
  transcription: string;
}

export type Transcriptions = Transcription[];
