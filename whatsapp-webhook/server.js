const express = require('express');
const axios = require('axios');
const ngrok = require('ngrok');
const sqlite3 = require('sqlite3').verbose();
require('dotenv').config();
const multer = require('multer');
const FormData = require('form-data');
const fs = require('fs');
const path = require('path');
const { handleBubbleChoice } = require('./narrative');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 3000;

// Add this near the top of your file with other global variables
let ngrokUrl = '';
const db = new sqlite3.Database('webhooks.db', (err) => {
  if (err) {
    console.error('Error opening database:', err);
  } else {
    console.log('Connected to SQLite database');
    // Create webhooks table if it doesn't exist
    db.run(`
      CREATE TABLE IF NOT EXISTS webhooks (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        payload TEXT,
        timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
      )
    `);
  }
});

// Configure multer for file upload
const upload = multer({
  dest: 'uploads/',
  limits: {
    fileSize: 100 * 1024 * 1024, // 100MB max file size
  }
});

// Webhook verification endpoint
app.get('/webhook', (req, res) => {
  const mode = req.query['hub.mode'];
  const token = req.query['hub.verify_token'];
  const challenge = req.query['hub.challenge'];

  if (mode && token) {
    if (mode === 'subscribe' && token === process.env.VERIFY_TOKEN) {
      console.log('Webhook verified');
      res.status(200).send(challenge);
    } else {
      console.log('Webhook verification failed');
      res.sendStatus(403);
    }
  }
});

// Webhook receiving endpoint
app.post('/webhook', (req, res) => {
  console.log('Received webhook:', JSON.stringify(req.body, null, 2));
  
  // Handle interactive responses
  if (req.body.entry?.[0]?.changes?.[0]?.value?.messages?.[0]?.interactive) {
    const interaction = req.body.entry[0].changes[0].value.messages[0].interactive;
    
    if (interaction.type === 'list_reply') {
      handleListResponse(interaction.list_reply.id);
    } else if (interaction.type === 'button_reply') {
      handleButtonResponse(interaction.button_reply.id);
    }
  }
  
  // Store webhook data and respond
  const payload = JSON.stringify(req.body);
  db.run('INSERT INTO webhooks (payload) VALUES (?)', [payload], (err) => {
    if (err) console.error('Error storing webhook:', err);
  });

  res.status(200).send('OK');
});

// Message sending endpoint
app.post('/send-message', async (req, res) => {
  try {
    const { text } = req.body;

    // Validate required fields
    if (!text) {
      return res.status(400).json({ error: 'Message text is required' });
    }

    // Trim according to WhatsApp limits
    const trimmedPayload = {
      text: text.slice(0, 4096)
    };

    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${process.env.WHATSAPP_NUMBER_ID}/messages`,
      {
        messaging_product: "whatsapp",
        to: process.env.TO_PHONE_NUMBER,
        type: "text",
        text: {
          body: trimmedPayload.text
        }
      },
      {
        headers: {
          'Authorization': `Bearer ${process.env.ACCESS_TOKEN}`,
          'Content-Type': 'application/json'
        }
      }
    );
    
    // Return both the API response and the trimmed values
    res.json({
      apiResponse: response.data,
      trimmedContent: trimmedPayload
    });
  } catch (error) {
    console.error('Error:', error.response?.data || error.message);
    res.status(500).json({ error: error.message });
  }
});

// Document sending endpoint
app.post('/send-media', async (req, res) => {
  try {
    const { media } = req.body;

    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${process.env.WHATSAPP_NUMBER_ID}/messages`,
      {
        messaging_product: "whatsapp",
        recipient_type: "individual",
        to: process.env.TO_PHONE_NUMBER,
        type: media.type,
        [media.type]: {
          id: media.id,
          caption: media.caption
        }
      },
      {
        headers: {
          'Authorization': `Bearer ${process.env.ACCESS_TOKEN}`,
          'Content-Type': 'application/json'
        }
      }
    );
    
    // Add response back to client
    res.json(response.data);
  } catch (error) {
    console.error('Error:', error.response?.data || error.message);
    res.status(500).json({ error: error.message });
  }
});

// interactive message button endpoint
app.post('/send-interactive-button', async (req, res) => {
  try {
    const { header, bodyText, footerText, buttons } = req.body;

    // Validate required fields
    if (!bodyText || !buttons || !Array.isArray(buttons) || buttons.length === 0) {
      return res.status(400).json({ error: 'Body text and at least one button are required' });
    }

    // Construct the message payload according to WhatsApp API format
    const messagePayload = {
      messaging_product: "whatsapp",
      recipient_type: "individual",
      to: process.env.TO_PHONE_NUMBER,
      type: "interactive",
      interactive: {
        type: "button",
        header: header && {
          type: header.type,
          text: header.text  // This must be non-null for text headers
        },
        body: {
          text: bodyText.slice(0, 1024)
        },
        footer: footerText ? {
          text: footerText.slice(0, 60)
        } : undefined,
        action: {
          buttons: buttons.slice(0, 3).map(button => ({
            type: "reply",
            reply: {
              id: button.id.slice(0, 256),
              title: button.title.slice(0, 20)
            }
          }))
        }
      }
    };

    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${process.env.WHATSAPP_NUMBER_ID}/messages`,
      messagePayload,
      {
        headers: {
          'Authorization': `Bearer ${process.env.ACCESS_TOKEN}`,
          'Content-Type': 'application/json'
        }
      }
    );
    
    res.json({
      apiResponse: response.data,
      sentPayload: messagePayload
    });
  } catch (error) {
    console.error('Error:', error.response?.data || error.message);
    res.status(500).json({ error: error.response?.data || error.message });
  }
});

// interactive message list endpoint
app.post('/send-interactive-list', async (req, res) => {
  try {
    const { headerText, bodyText, footerText, sections, buttonText } = req.body;

    // Validate required fields
    if (!bodyText || !sections || !Array.isArray(sections) || sections.length === 0 || !buttonText) {
      return res.status(400).json({ 
        error: 'Body text, button text, and at least one section with rows are required' 
      });
    }

    // Trim and validate according to WhatsApp limits
    const trimmedPayload = {
      buttonText: buttonText.slice(0, 20),
      bodyText: bodyText.slice(0, 4096),
      headerText: headerText?.slice(0, 60),
      footerText: footerText?.slice(0, 60),
      sections: sections.slice(0, 10).map(section => ({
        title: section.title.slice(0, 24),
        rows: section.rows.slice(0, 10).map(row => ({
          id: row.id.slice(0, 200),
          title: row.title.slice(0, 24),
          description: row.description?.slice(0, 72)
        }))
      }))
    };

    // Construct the message payload
    const messagePayload = {
      messaging_product: "whatsapp",
      recipient_type: "individual",
      to: process.env.TO_PHONE_NUMBER,
      type: "interactive",
      interactive: {
        type: "list",
        body: {
          text: trimmedPayload.bodyText
        },
        action: {
          button: trimmedPayload.buttonText,
          sections: trimmedPayload.sections
        }
      }
    };

    // Add header if provided
    if (trimmedPayload.headerText) {
      messagePayload.interactive.header = {
        type: "text",
        text: trimmedPayload.headerText
      };
    }

    // Add footer if provided
    if (trimmedPayload.footerText) {
      messagePayload.interactive.footer = {
        text: trimmedPayload.footerText
      };
    }

    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${process.env.WHATSAPP_NUMBER_ID}/messages`,
      messagePayload,
      {
        headers: {
          'Authorization': `Bearer ${process.env.ACCESS_TOKEN}`,
          'Content-Type': 'application/json'
        }
      }
    );
    
    // Return both the API response and the trimmed values
    res.json({
      apiResponse: response.data,
      trimmedContent: trimmedPayload
    });
  } catch (error) {
    console.error('Error:', error.response?.data || error.message);
    res.status(500).json({ error: error.message });
  }
});

// Send carousel template endpoint
app.post('/send-carousel', async (req, res) => {
  try {
    const { templateName, cards } = req.body;

    // Validate required fields
    if ( !templateName || !cards || !Array.isArray(cards)) {
      return res.status(400).json({ 
        error: 'Template name, and cards array are required' 
      });
    }

    const messagePayload = {
      messaging_product: "whatsapp",
      recipient_type: "individual",
      to: process.env.TO_PHONE_NUMBER,
      type: "template",
      template: {
        name: templateName,
        language: {
          code: "en_US"
        },
        components: [
          {
            type: "body",
            parameters: [
              {
                type: "text",
                text: "Limited time offer!"  // Parameter {{1}}
              },
              {
                type: "text",
                text: "SAVE20"  // Parameter {{2}}
              }
            ]
          },
          {
            type: "carousel",
            cards: cards.map((card, index) => ({
              card_index: index,
              components: [
                {
                  type: "header",
                  parameters: [
                    {
                      type: "image",
                      image: {
                        id: card.imageId
                      }
                    }
                  ]
                },
                {
                  type: "button",
                  sub_type: "url",
                  index: 0,
                  parameters: [
                    {
                      type: "text",
                      text: card.buttonText
                    }
                  ]
                }
              ]
            }))
          }
        ]
      }
    };

    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${process.env.WHATSAPP_NUMBER_ID}/messages`,
      messagePayload,
      {
        headers: {
          'Authorization': `Bearer ${process.env.ACCESS_TOKEN}`,
          'Content-Type': 'application/json'
        }
      }
    );

    res.json({
      success: true,
      apiResponse: response.data,
      sentPayload: messagePayload
    });
  } catch (error) {
    console.error('Error sending carousel:', error.response?.data || error.message);
    res.status(500).json({ 
      success: false, 
      error: error.response?.data || error.message 
    });
  }
});

// Media upload endpoint
app.post('/upload-media', upload.single('file'), async (req, res) => {
  try {
    if (!req.file) {
      return res.status(400).json({ error: 'No file uploaded' });
    }

    // Create form data
    const form = new FormData();
    form.append('messaging_product', 'whatsapp');
    form.append('file', fs.createReadStream(req.file.path), {
      filename: req.file.originalname,
      contentType: req.file.mimetype,
    });

    // Send request to WhatsApp API
    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${process.env.WHATSAPP_NUMBER_ID}/media`,
      form,
      {
        headers: {
          'Authorization': `Bearer ${process.env.ACCESS_TOKEN}`,
          ...form.getHeaders(),
        }
      }
    );

    // Clean up: delete the uploaded file
    fs.unlink(req.file.path, (err) => {
      if (err) console.error('Error deleting temporary file:', err);
    });

    res.json(response.data);
  } catch (error) {
    console.error('Error uploading media:', error.response?.data || error.message);
    res.status(500).json({ error: error.response?.data || error.message });
  }
});

// Conversational commands endpoint
// @deprecated these are broken at a facebook level and they have no timeline for them to be resolved
app.post('/set-conversational-commands', async (req, res) => {
  try {
    const { enableWelcomeMessage, commands, prompts } = req.body;

    // Validate required fields
    if (!commands || !Array.isArray(commands) || commands.length === 0) {
      return res.status(400).json({ 
        error: 'At least one command is required' 
      });
    }

    // Validate command structure
    for (const command of commands) {
      if (!command.command_name || !command.command_description) {
        return res.status(400).json({
          error: 'Each command must have a command_name and command_description'
        });
      }
    }

    const messagePayload = {
      enable_welcome_message: !!enableWelcomeMessage, // Convert to boolean
      commands: commands.map(command => ({
        command_name: command.command_name,
        command_description: command.command_description
      }))
    };

    // Add prompts if provided
    if (prompts && Array.isArray(prompts)) {
      messagePayload.prompts = prompts;
    }

    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${process.env.WHATSAPP_NUMBER_ID}/conversational_automation`,
      messagePayload,
      {
        headers: {
          'Authorization': `Bearer ${process.env.ACCESS_TOKEN}`,
          'Content-Type': 'application/json'
        }
      }
    );

    res.json({
      success: true,
      apiResponse: response.data,
      sentPayload: messagePayload
    });
  } catch (error) {
    console.error('Error setting conversational commands:', error.response?.data || error.message);
    res.status(500).json({ 
      success: false, 
      error: error.response?.data || error.message 
    });
  }
});

// Start server and ngrok
const startServer = async () => {
  try {
    // Start the Express server first
    app.listen(PORT, () => {
      console.log(`Server running on port ${PORT}`);
      console.log(`Local URL: http://localhost:${PORT}`);
    });

    console.log(`Server URL: ${url}`);
  } catch (error) {
    console.error('Error:', error.message);
  }
};

// Add an endpoint to retrieve webhook history
app.get('/webhook-history', (req, res) => {
  db.all('SELECT * FROM webhooks ORDER BY timestamp DESC LIMIT 100', [], (err, rows) => {
    if (err) {
      console.error('Error retrieving webhooks:', err);
      res.status(500).json({ error: err.message });
    } else {
      res.json(rows);
    }
  });
});

// Update graceful shutdown to close database
process.on('SIGTERM', async () => {
  db.close((err) => {
    if (err) {
      console.error('Error closing database:', err);
    } else {
      console.log('Database connection closed');
    }
  });
  await ngrok.kill();
  process.exit(0);
});

// Update the /url endpoint to use environment variables
app.get('/url', (req, res) => {
  const baseUrl = process.env.NGROK_URL;
  res.json({
    url: baseUrl,
    webhookUrl: `${baseUrl}/webhook`,
    localUrl: `http://localhost:${PORT}`
  });
});

// Update the helper functions
async function handleListResponse(choiceId) {
  try {
    await handleBubbleChoice(choiceId);
  } catch (error) {
    console.error('Error handling list response:', error);
  }
}

async function handleButtonResponse(choiceId) {
  // Add more narrative branches based on button responses
  const responses = {
    investigate_ship: "You approach the mysterious spacecraft...",
    enter_vortex: "You dive into the swirling temporal vortex...",
    hack_bot: "You attempt to hack the trading bot...",
    escape_crash: "You rush towards the exit as markets tumble...",
    meet_people: "You shrink down to meet the tiny civilization...",
    use_wand: "You grasp the magical bubble wand..."
  };

  try {
    await axios.post('http://localhost:3000/send-message', {
      text: responses[choiceId] || "Something unexpected happened..."
    });
  } catch (error) {
    console.error('Error handling button response:', error);
  }
}

startServer(); 