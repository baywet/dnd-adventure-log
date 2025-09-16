
export interface Player {
  name: string;
  description: string;
  portraitUrl?: string;
}

export enum ProcessState {
  Idle,
  Processing,
  Success,
  Error,
}
