
import React, { useState, useCallback } from 'react';
import { Header } from './components/Header';
import { FileUpload } from './components/FileUpload';
import { ProcessingView } from './components/ProcessingView';
import { ResultsView } from './components/ResultsView';
import { transcribeAudio, analyzeTranscriptForPlayers, generatePlayerPortrait, findEpicMoment, generateEpicVideo } from './services/geminiService';
import type { Player } from './types';
import { ProcessState } from './types';

const App: React.FC = () => {
  const [processState, setProcessState] = useState<ProcessState>(ProcessState.Idle);
  const [loadingMessage, setLoadingMessage] = useState<string>('');
  const [error, setError] = useState<string | null>(null);

  const [uploadedFiles, setUploadedFiles] = useState<File[]>([]);
  const [transcript, setTranscript] = useState<string>('');
  const [players, setPlayers] = useState<Player[]>([]);
  const [epicMomentVideoUrl, setEpicMomentVideoUrl] = useState<string | null>(null);
  
  const resetState = useCallback(() => {
    setProcessState(ProcessState.Idle);
    setLoadingMessage('');
    setError(null);
    setTranscript('');
    setPlayers([]);
    setEpicMomentVideoUrl(null);
    setUploadedFiles([]);
  }, []);

  const handleFilesUpload = useCallback(async (files: File[]) => {
    // Clear previous results and set processing state
    setProcessState(ProcessState.Processing);
    setError(null);
    setTranscript('');
    setPlayers([]);
    setEpicMomentVideoUrl(null);
    setUploadedFiles(files);

    try {
      // Step 1: Transcription
      const transcripts: string[] = [];
      for (let i = 0; i < files.length; i++) {
        const file = files[i];
        setLoadingMessage(`Transcribing audio log ${i + 1} of ${files.length}: ${file.name}...`);
        const partTranscript = await transcribeAudio(file);
        transcripts.push(partTranscript);
      }
      const generatedTranscript = transcripts.join('\n\n---\n\n');
      setTranscript(generatedTranscript);


      // Step 2: Identify Players
      setLoadingMessage('Consulting the arcane orbs to identify the heroes...');
      const playersToVisualize = await analyzeTranscriptForPlayers(generatedTranscript);
      if (playersToVisualize.length === 0) {
        throw new Error("The ancient texts revealed no clear heroes. Please try another recording.");
      }
      
      // Step 3: Generate Portraits
      setLoadingMessage(`Capturing the essence of ${playersToVisualize.length} heroes onto canvas...`);
      const portraitPromises = playersToVisualize.map(async (player) => {
        const imageData = await generatePlayerPortrait(player.description);
        return { name: player.name, description: player.description, portraitUrl: `data:image/png;base64,${imageData}` };
      });
      const generatedPlayers = await Promise.all(portraitPromises);
      setPlayers(generatedPlayers);

      // Step 4: Find Epic Moment
      setLoadingMessage('Scouring the chronicles for the most epic moment...');
      const epicMomentPrompt = await findEpicMoment(generatedTranscript);

      // Step 5: Generate Epic Video
      setLoadingMessage('Weaving the threads of destiny into a moving picture... (This may take several minutes)');
      const videoUrl = await generateEpicVideo(epicMomentPrompt, (message) => {
         setLoadingMessage(message);
      });
      setEpicMomentVideoUrl(videoUrl);

      setProcessState(ProcessState.Success);
    } catch (err) {
      console.error(err);
      setError(err instanceof Error ? err.message : 'An unknown magical interference occurred.');
      setProcessState(ProcessState.Error);
    } finally {
      setLoadingMessage('');
    }
  }, []);

  const handleDeleteFile = useCallback((indexToDelete: number) => {
    const remainingFiles = uploadedFiles.filter((_, index) => index !== indexToDelete);
    if (remainingFiles.length > 0) {
        handleFilesUpload(remainingFiles);
    } else {
        resetState();
    }
  }, [uploadedFiles, handleFilesUpload, resetState]);

  return (
    <div className="min-h-screen bg-gray-900 text-gray-200">
      <Header />
      <main className="container mx-auto px-4 py-8 md:py-12">
        <div className="max-w-4xl mx-auto">
          {processState === ProcessState.Idle && <FileUpload onFilesUpload={handleFilesUpload} />}
          {processState === ProcessState.Processing && <ProcessingView message={loadingMessage} />}
          {processState === ProcessState.Success && (
            <ResultsView 
              files={uploadedFiles}
              onDeleteFile={handleDeleteFile}
              transcript={transcript}
              players={players}
              epicMomentVideoUrl={epicMomentVideoUrl}
              onReset={resetState}
            />
          )}
          {(processState === ProcessState.Error) && (
             <div className="text-center p-8 bg-gray-800 border border-red-500 rounded-lg">
              <h2 className="text-2xl font-title text-red-400 mb-4">A Critical Failure!</h2>
              <p className="text-lg mb-6">{error}</p>
              <button
                onClick={resetState}
                className="bg-yellow-600 text-gray-900 font-bold py-2 px-6 rounded-lg hover:bg-yellow-500 transition-colors duration-300"
              >
                Try Again
              </button>
            </div>
          )}
        </div>
      </main>
    </div>
  );
};

export default App;
