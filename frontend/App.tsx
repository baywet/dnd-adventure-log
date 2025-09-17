import React, { useState, useCallback } from "react";
import { Header } from "./components/Header";
import { FileUpload } from "./components/FileUpload";
import { ProcessingView } from "./components/ProcessingView";
import { ResultsView } from "./components/ResultsView";
import { ApiService } from "./services/api.service";
import type { Player, Players, Transcription, Transcriptions } from "./types";
import { ProcessState } from "./types";
import { ApiAxiomService } from "./services/api.axiom.service";

const App: React.FC = () => {
  const [processState, setProcessState] = useState<ProcessState>(
    ProcessState.Idle
  );
  const [loadingMessage, setLoadingMessage] = useState<string>("");
  const [error, setError] = useState<string | null>(null);

  const [uploadedFiles, setUploadedFiles] = useState<File[]>([]);
  const [transcript, setTranscript] = useState<string>("");
  const [players, setPlayers] = useState<Player[]>([]);
  const [epicMomentVideoUrl, setEpicMomentVideoUrl] = useState<string | null>(
    null
  );

  const resetState = useCallback(() => {
    setProcessState(ProcessState.Idle);
    setLoadingMessage("");
    setError(null);
    setTranscript("");
    setPlayers([]);
    setEpicMomentVideoUrl(null);
    setUploadedFiles([]);
  }, []);

  const handleFilesUpload = useCallback(async (files: File[]) => {
    // Clear previous results and set processing state
    setProcessState(ProcessState.Processing);
    setError(null);
    setTranscript("");
    setPlayers([]);
    setEpicMomentVideoUrl(null);
    setUploadedFiles(files);

    try {
      setLoadingMessage("Asking our scrybe to write down the legend...");
      setProcessState(ProcessState.Processing);

      // 1. Upload recording
      const transcriptResult = await ApiService.uploadRecording(files).catch((err) => {
        console.error("Failed to upload recordings:", err);
        setProcessState(ProcessState.Error);
        return [];
      });
      setTranscript(transcriptResult.map((t) => t.transcription).join("\n") || "");

      // Assume the API returns the recording name in the result, or derive it from the file name
      // For this example, use the first file's name (adjust as needed)
      const recordingName = files[0]?.name;
      if (!recordingName) throw new Error("No recording name found.");

      // 2. POST /characters to generate data for this recording
      setLoadingMessage("Summoning the heroes from the legend...");
      await ApiService.generateCharacters(recordingName);

      // 3. List characters for this recording
      setLoadingMessage("Consulting the arcane orbs to identify the heroes...");
      const playersResult = await ApiService.listCharacters(recordingName).catch((err) => {
        console.error("Failed to get players:", err);
        setProcessState(ProcessState.Error);
        return [];
      });
      setPlayers(playersResult);

      // 4. For each player, POST character profile to generate portrait
      setLoadingMessage("Capturing the essence of each hero onto canvas...");
      await Promise.all(
        playersResult.map(async (player) => {
          try {
            await ApiService.getPlayerPortrait(recordingName, player.name);
          } catch (err) {
            console.error(`Failed to generate portrait for ${player.name}:`, err);
          }
        })
      );

      setProcessState(ProcessState.Success);
    } catch (err) {
      console.error(err);
      setError(
        err instanceof Error
          ? err.message
          : "An unknown magical interference occurred."
      );
      setProcessState(ProcessState.Error);
    } finally {
      setLoadingMessage("");
    }
  }, []);

  const handleDeleteFile = useCallback(
    (indexToDelete: number) => {
      const remainingFiles = uploadedFiles.filter(
        (_, index) => index !== indexToDelete
      );
      if (remainingFiles.length > 0) {
        handleFilesUpload(remainingFiles);
      } else {
        resetState();
      }
    },
    [uploadedFiles, handleFilesUpload, resetState]
  );

  return (
    <div className="min-h-screen bg-gray-900 text-gray-200">
      <Header />
      <main className="container mx-auto px-4 py-8 md:py-12">
        <div className="max-w-4xl mx-auto">
          {processState === ProcessState.Idle && (
            <FileUpload onFilesUpload={handleFilesUpload} />
          )}
          {processState === ProcessState.Processing && (
            <ProcessingView message={loadingMessage} />
          )}
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
          {processState === ProcessState.Error && (
            <div className="text-center p-8 bg-gray-800 border border-red-500 rounded-lg">
              <h2 className="text-2xl font-title text-red-400 mb-4">
                A Critical Failure!
              </h2>
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
