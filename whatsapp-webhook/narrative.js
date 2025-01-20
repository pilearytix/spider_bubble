const axios = require('axios');
const fs = require('fs');
const path = require('path');

class StoryManager {
  constructor() {
    this.playerStates = new Map(); // Store player states
    this.storyPath = path.join(__dirname, 'story');
  }

  async loadScene(sceneId) {
    try {
      const scenePath = path.join(this.storyPath, 'scenes', sceneId + '.json');
      const sceneData = JSON.parse(fs.readFileSync(scenePath, 'utf8'));
      return sceneData;
    } catch (error) {
      console.error(`Error loading scene ${sceneId}:`, error);
      // Load error scene if available, otherwise throw
      const errorPath = path.join(this.storyPath, 'scenes', 'common', 'error.json');
      if (fs.existsSync(errorPath)) {
        return JSON.parse(fs.readFileSync(errorPath, 'utf8'));
      }
      throw error;
    }
  }

  async handleScene(sceneId, playerId) {
    try {
      // Initialize player state if it doesn't exist
      if (!this.playerStates.has(playerId)) {
        this.playerStates.set(playerId, {
          currentScene: sceneId,
          inventory: [],
          visited: new Set()
        });
      }

      const playerState = this.playerStates.get(playerId);
      playerState.visited.add(sceneId);
      
      const sceneContent = await this.loadScene(sceneId);
      const response = await axios.post('http://localhost:3000/send-interactive-list', sceneContent);
      console.log(`Scene ${sceneId} handled:`, response.data);
      return response.data;
    } catch (error) {
      console.error('Error handling scene:', error.response?.data || error.message);
      throw error;
    }
  }

  async handleChoice(playerId, choiceId) {
    try {
      const playerState = this.playerStates.get(playerId);
      if (!playerState) {
        throw new Error('Player state not found');
      }

      const choicesPath = path.join(this.storyPath, 'choices.json');
      const choices = JSON.parse(fs.readFileSync(choicesPath, 'utf8'));
      const choice = choices[choiceId] || choices.default;

      // Update player state based on choice
      if (choice.effects) {
        if (choice.effects.addItem) playerState.inventory.push(choice.effects.addItem);
        if (choice.effects.nextScene) playerState.currentScene = choice.effects.nextScene;
      }

      const response = await axios.post('http://localhost:3000/send-interactive-button', choice.message);
      console.log('Choice handled:', response.data);
      return response.data;
    } catch (error) {
      console.error('Error handling choice:', error.response?.data || error.message);
      throw error;
    }
  }

  getPlayerState(playerId) {
    return this.playerStates.get(playerId);
  }

  async startNewGame(playerId) {
    try {
      // Reset player state
      this.playerStates.set(playerId, {
        currentScene: 'intro/welcome',
        inventory: [],
        visited: new Set()
      });

      return await this.handleScene('intro/welcome', playerId);
    } catch (error) {
      console.error('Error starting new game:', error);
      throw error;
    }
  }
}

// Create and export a single instance
const storyManager = new StoryManager();
module.exports = storyManager;