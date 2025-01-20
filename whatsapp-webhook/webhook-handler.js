const storyManager = require('./narrative');

async function handleWebhook(req, res) {
  const { from, button, interactive } = req.body;
  
  try {
    if (button) {
      await storyManager.handleChoice(from, button.id);
    } else if (interactive) {
      const choiceId = interactive.list_reply?.id || interactive.button_reply?.id;
      if (choiceId) {
        await storyManager.handleChoice(from, choiceId);
      }
    } else {
      // New user or restart
      await storyManager.handleScene('intro_welcome', from);
    }
    
    res.status(200).json({ success: true });
  } catch (error) {
    console.error('Webhook handler error:', error);
    res.status(500).json({ error: 'Internal server error' });
  }
}

module.exports = handleWebhook; 