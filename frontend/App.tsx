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
} from "./types";
import { ProcessState } from "./types";
import { API_BASE_URL, ApiAxiomService } from "./services/api.axiom.service";
import { CampaignList } from "./components/CampaignList";
import { PlayerPortrait } from "./components/PlayerPortrait";
import { EpicMomentVideo } from "./components/EpicMomentVideo";

const App: React.FC = () => {
  const [processState, setProcessState] = useState<ProcessState>(
    ProcessState.Idle
  );
  const [loadingMessage, setLoadingMessage] = useState<string>("");
  const [error, setError] = useState<string | null>(null);

  const [uploadedFiles, setUploadedFiles] = useState<File[]>([]);
  const [transcript, setTranscript] = useState<string>("");
  const [players, setPlayers] = useState<Player[]>([]);
  const [epicMomentVideoUrls, setEpicMomentVideoUrls] =
    useState<string[]>(null);

  const resetState = useCallback(() => {
    setProcessState(ProcessState.Idle);
    setLoadingMessage("");
    setSelectedCampaign(null);
    setError(null);
    setTranscript("");
    setPlayers([] as Player[]);
    setEpicMomentVideoUrls([]);
    setUploadedFiles([]);
  }, []);

  const [selectedCampaign, setSelectedCampaign] = useState<Campaign | null>(
    null
  );

const handleSelectCampaign = useCallback((campaign: Campaign) => {
  resetState(); // Reset everything
  setSelectedCampaign(campaign); // Then set the new campaign
}, [resetState]);

  const handleFilesUpload = useCallback(
    async (files: File[]) => {
      // Clear previous results and set processing state
      setProcessState(ProcessState.Processing);
      setError(null);
      setTranscript("");
      setPlayers([] as Player[]);
      setEpicMomentVideoUrls([]);
      setUploadedFiles(files);

      try {
        setProcessState(ProcessState.Processing);

        if (!selectedCampaign || !selectedCampaign.name) {
          setError("Please select a campaign before starting the ritual.");
          return;
        }

        // Transcribe the audio files
        setLoadingMessage("Asking our scrybe to write down the legend...");
        await ApiAxiomService.uploadRecording(selectedCampaign.name, files);

        setLoadingMessage(
          "Consulting the arcane orbs to identify the heroes..."
        );
        let players = await ApiAxiomService.generateCharacters(selectedCampaign.name);
        //let players: Player[] = [];

        if (!players || players.length === 0) {
          players = await ApiAxiomService.listCharacters(selectedCampaign.name);
        }

        for (const [i, player] of players.entries()) {
          setLoadingMessage(
            `Painting portraits of the heroes (${i + 1}/${players.length})...`
          );
          await ApiAxiomService.generateCharacterProfile(
            selectedCampaign.name,
            player.name
          );
        }

        setPlayers(players);

        var recordings = await ApiAxiomService.listRecordings(
          selectedCampaign.name
        );

        setLoadingMessage("Summoning the epic moment from the ether...");
        for (const rec of recordings) {
          await ApiAxiomService.createEpicMoment(
            selectedCampaign.name,
            rec
          ).catch((err) => { console.error("The weave falters! The epic moment could not be conjured for recording:", rec, err); });
          setEpicMomentVideoUrls((prev) => [
            ...prev,
            `${API_BASE_URL}/campaigns/${encodeURIComponent(
              selectedCampaign.name
            )}/recordings/${encodeURIComponent(rec)}/epic-moment`,
          ]);
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
    },
    [selectedCampaign]
  );

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
      <Header onReset={resetState} />
      <main className="container mx-auto px-4 py-8 md:py-12">
        <div>
          <CampaignList onSelect={handleSelectCampaign} selected={selectedCampaign}></CampaignList>
        </div>
        <div className="max-w-4xl mx-auto space-y-12">
          {processState === ProcessState.Idle && selectedCampaign && (
            <FileUpload onFilesUpload={handleFilesUpload} />
          )}

          {processState === ProcessState.Processing && (
            <ProcessingView message={loadingMessage} />    
          )}

          {/* Heroes Gallery */}
          {selectedCampaign && players && players.length !== 0 && (
              <section>
              <h2 className="text-4xl font-title text-center text-yellow-400 mb-8">
                The Heroes of the Quest
              </h2>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-8">
                {players.map((player) => (
                  <PlayerPortrait
                    campaign={selectedCampaign?.name}
                    key={player.name}
                    player={player}
                  />
                ))}
              </div>
            </section>
          )}

          {/* Epic Moment */}
          {epicMomentVideoUrls && epicMomentVideoUrls.length !== 0 &&(
            <section>
              <h2 className="text-4xl font-title text-center text-yellow-400 mb-8">
                The Epic Climax
              </h2>
              <EpicMomentVideo epicMomentVideoUrls={epicMomentVideoUrls} />
            </section>
          )}

          {/* {processState === ProcessState.Success && (
 
          )} */}
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
