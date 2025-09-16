
import { GoogleGenAI, Type } from "@google/genai";
import type { Player } from '../types';

if (!process.env.API_KEY) {
    throw new Error("API_KEY environment variable not set");
}

const ai = new GoogleGenAI({ apiKey: process.env.API_KEY });

const fileToBase64 = (file: File): Promise<string> => {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.readAsDataURL(file);
        reader.onload = () => {
            const result = reader.result as string;
            // remove "data:audio/mpeg;base64," prefix
            const base64 = result.split(',')[1];
            resolve(base64);
        };
        reader.onerror = (error) => reject(error);
    });
};

export const transcribeAudio = async (file: File): Promise<string> => {
    const base64Audio = await fileToBase64(file);
    
    const audioPart = {
        inlineData: {
            mimeType: file.type,
            data: base64Audio,
        },
    };

    const textPart = {
        text: "You are an expert transcriber specializing in tabletop roleplaying game sessions like Dungeons & Dragons. Transcribe the following audio recording. Make sure to identify and label different speakers if possible, such as 'DM' for the Dungeon Master and player character names if they are mentioned.",
    };

    try {
        const response = await ai.models.generateContent({
            model: 'gemini-2.5-flash',
            contents: { parts: [audioPart, textPart] },
        });

        if(!response.text) {
             throw new Error("Transcription resulted in empty text.");
        }
        return response.text;
    } catch (error) {
        console.error("Error transcribing audio:", error);
        throw new Error("The scryers failed to understand the audio. Please provide a clearer recording.");
    }
};

export const analyzeTranscriptForPlayers = async (transcript: string): Promise<Player[]> => {
    try {
        const response = await ai.models.generateContent({
            model: "gemini-2.5-flash",
            contents: `Analyze the following Dungeons & Dragons session transcript. Identify up to 3 main player characters. For each character, provide their name and a concise, one-sentence visual description suitable for an AI image generator. Do not include the Dungeon Master (DM). Focus on physical appearance, class, race, and notable gear mentioned.

Transcript:
---
${transcript}
---`,
            config: {
                responseMimeType: "application/json",
                responseSchema: {
                    type: Type.ARRAY,
                    items: {
                        type: Type.OBJECT,
                        properties: {
                            name: { type: Type.STRING },
                            description: { type: Type.STRING }
                        },
                        required: ["name", "description"]
                    }
                }
            }
        });

        const jsonString = response.text;
        const players = JSON.parse(jsonString);
        return players;
    } catch (error) {
        console.error("Error analyzing transcript for players:", error);
        throw new Error("Failed to identify heroes from the transcript.");
    }
};

export const generatePlayerPortrait = async (description: string): Promise<string> => {
    try {
        const fullPrompt = `Fantasy character portrait, digital painting, epic, detailed. A D&D character: ${description}.`;
        const response = await ai.models.generateImages({
            model: 'imagen-4.0-generate-001',
            prompt: fullPrompt,
            config: {
                numberOfImages: 1,
                outputMimeType: 'image/png',
                aspectRatio: '3:4',
            },
        });

        if (!response.generatedImages || response.generatedImages.length === 0) {
            throw new Error("Image generation failed to produce an image.");
        }

        return response.generatedImages[0].image.imageBytes;
    } catch (error) {
        console.error("Error generating player portrait:", error);
        throw new Error("The portrait artist seems to be on a break. Failed to generate image.");
    }
};

export const findEpicMoment = async (transcript: string): Promise<string> => {
    try {
        const response = await ai.models.generateContent({
            model: "gemini-2.5-flash",
            contents: `From the following D&D transcript, identify the single most visually epic and climactic moment. Describe this moment in a single, vivid sentence that can be used as a prompt for an AI video generator. The description should focus on action and scenery. For example: 'A valiant knight in glowing armor raises his gleaming sword to strike a roaring black dragon amidst a fiery cavern.'

Transcript:
---
${transcript}
---`
        });
        return response.text.trim();
    } catch (error) {
        console.error("Error finding epic moment:", error);
        throw new Error("Failed to find the epic moment in the transcript.");
    }
};

const VIDEO_GENERATION_MESSAGES = [
  "Channelling raw magic into the vision...",
  "The cinematic energies are coalescing...",
  "Rendering the epic moment frame by frame...",
  "Polishing the final cut, adding dragon roars...",
  "Almost there! The legend is about to come to life..."
];

export const generateEpicVideo = async (prompt: string, onUpdate: (message: string) => void): Promise<string> => {
    try {
        let operation = await ai.models.generateVideos({
            model: 'veo-2.0-generate-001',
            prompt: `Epic cinematic 5-second shot, fantasy, high detail, dramatic lighting. ${prompt}`,
            config: {
                numberOfVideos: 1,
            }
        });
        
        let messageIndex = 0;
        while (!operation.done) {
            onUpdate(VIDEO_GENERATION_MESSAGES[messageIndex % VIDEO_GENERATION_MESSAGES.length]);
            messageIndex++;
            await new Promise(resolve => setTimeout(resolve, 10000));
            operation = await ai.operations.getVideosOperation({ operation: operation });
        }

        const downloadLink = operation.response?.generatedVideos?.[0]?.video?.uri;
        if (!downloadLink) {
            throw new Error("Video generation completed, but no download link was found.");
        }
        
        // In a real browser environment, you would fetch and create a blob URL.
        // For this app, we'll return the authenticated link directly.
        // Note: This link might have a short expiry.
        return `${downloadLink}&key=${process.env.API_KEY}`;

    } catch (error) {
        console.error("Error generating epic video:", error);
        throw new Error("The vision failed to materialize. Video generation was unsuccessful.");
    }
};
