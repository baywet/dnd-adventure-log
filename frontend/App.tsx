import React, { useState, useCallback } from "react";
import { Header } from "./components/Header";
import { FileUpload } from "./components/FileUpload";
import { ProcessingView } from "./components/ProcessingView";
import { ResultsView } from "./components/ResultsView";
import { ApiService } from "./services/api.service";
import type {
  Campaign,
  Player,
  Players,
  Transcription,
  Transcriptions,
} from "./types";
import { ProcessState } from "./types";
import { ApiAxiomService } from "./services/api.axiom.service";
import { CampaignList } from "./components/CampaignList";

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

  const [selectedCampaign, setSelectedCampaign] = useState<Campaign | null>(
    null
  );

  const handleFilesUpload = useCallback(async (files: File[]) => {
    // Clear previous results and set processing state
    setProcessState(ProcessState.Processing);
    setError(null);
    setTranscript("");
    setPlayers([]);
    setEpicMomentVideoUrl(null);
    setUploadedFiles(files);

    try {
      setProcessState(ProcessState.Processing);

      // Transcribe the audio files
      setLoadingMessage("Asking our scrybe to write down the legend...");
      await ApiAxiomService.uploadRecording(
        selectedCampaign?.name || "",
        files
      );

      setLoadingMessage("Consulting the arcane orbs to identify the heroes...");
      await ApiAxiomService.generateCharacters(selectedCampaign?.name || "");

      await ApiAxiomService.listCharacters(
        selectedCampaign?.name || ""
      ).then((chars: Players) => {
        setPlayers(chars);
      });

     // call api axiome service to generate profile for each player
     setLoadingMessage("Forging the heroes profiles in the fires of Mount Doom...");
      for (const player of players) {
        await ApiAxiomService.generateCharacterProfile(
          selectedCampaign?.name || "",
          player.name
        );
      }

      await ApiAxiomService.listRecordings(
        selectedCampaign?.name || ""
      ).then((transcs: Transcriptions) => {
        const fullTranscript = transcs.map((t) => t.transcription).join("\n");
        setTranscript(fullTranscript);
      });

      setLoadingMessage("Summoning the epic moment from the ether...");
      // for now just get the first recording and generate epic moment for it
      if (files.length > 0) {
        const epicMoment = await ApiAxiomService.createEpicMoment(
          selectedCampaign?.name || "",
          files[0].name
        );
        setEpicMomentVideoUrl(epicMoment?.epicMomentUrl || null);
      }

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
        <div>
          <CampaignList onSelect={setSelectedCampaign}></CampaignList>
        </div>
        <div className="max-w-4xl mx-auto">
          {processState === ProcessState.Idle && selectedCampaign != null && (
            <FileUpload
              campaign={selectedCampaign}
              onFilesUpload={handleFilesUpload}
            />
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
