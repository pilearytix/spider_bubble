const axios = require('axios');
const fs = require('fs');
const FormData = require('form-data');
const storyManager = require('./narrative');

const testWebhook = async () => {
  try {
    const response = await axios.post('http://localhost:3000/send-message', {
      text: 'Hello, this is a test message'
    });
    console.log('Response:', response.data);
  } catch (error) {
    console.error('Error:', error.response?.data || error.message);
  }
};

async function testImageUpload() {
  const form = new FormData();
  form.append('file', fs.createReadStream('./testing/assets/image.png'));
  
  try {
    const response = await axios.post('http://localhost:3000/upload-media', form, {
      headers: {
        ...form.getHeaders()
      }
    });
    console.log('Media uploaded successfully:', response.data);
    return response.data.id; // Returns the media ID for use in messages
  } catch (error) {
    console.error('Error uploading media:', error.response?.data || error.message);
  }
}

async function testVideoUpload() {
  const form = new FormData();
  form.append('file', fs.createReadStream('./testing/assets/video.mp4'));
  
  try {
    const response = await axios.post('http://localhost:3000/upload-media', form, {
      headers: {
        ...form.getHeaders()
      }
    });
    console.log('Media uploaded successfully:', response.data);
    return response.data.id; // Returns the media ID for use in messages
  } catch (error) {
    console.error('Error uploading media:', error.response?.data || error.message);
  }
}

// Test sending a video
async function testSendVideo(videoId) {
  try {
    const response = await axios.post('http://localhost:3000/send-media', {
      media: {
        type: "video",
        id: videoId,
        caption: "This is a test video"
      }
    });
  } catch (error) {
    console.error('Error sending video:', error.response?.data || error.message);
  }
}

// Test sending an interactive message
async function testSendInteractiveButtonWithImageHeader(imageId) {
  try {
    const response = await axios.post('http://localhost:3000/send-interactive-button', {
      header: {
        type: "image",
        mediaId: imageId  // Move mediaId to the header level
      },
      bodyText: "Would you like to proceed?",
      footerText: "Choose an option below",
      buttons: [
        {
          id: "btn_yes",
          title: "Yes, continue"
        },
        {
          id: "btn_no",
          title: "No, thanks"
        }
      ]
    });

    console.log('Interactive message sent successfully:', response.data);
  } catch (error) {
    console.error('Error sending interactive message:', error.response?.data || error.message);
  }
}

// Test sending an interactive message
async function testSendInteractiveList(videoId) {
  try {
    const response = await axios.post('http://localhost:3000/send-interactive-list', {
      headerText: 'this is the header text',
      bodyText: "Would you like to proceed?",
      footerText: "This is the footer",
      buttonText: "Choose an option beloasdasasasda",
      sections: [
        {
          title: "Section 1",
          rows: [
            {
              id: "row_1",
              title: "Row 1",
              description: "Description 1"
            },
            {
              id: "row_2",
              title: "Row 2",
              description: "Description 2"
            }
          ]
        },
        {
          title: "Section 2",
          rows: [
            {
              id: "row_3",
              title: "Row 3",
              description: "Arbitrary string identifying the row. This ID will be included in the webhook payload if the user submits the selection."
            }
          ]
        }
      ]
    });

    console.log('Interactive message sent successfully:', response.data);
  } catch (error) {
    console.error('Error sending interactive message:', error.response?.data || error.message);
  }
}

// Test sending a carousel message
async function testSendCarousel(imageId) {
  try {
    const response = await axios.post('http://localhost:3000/send-carousel', {
      templateName: "product_carousel_template",
      bodyParameters: [
        "Limited time offer!",  // Parameter {{1}}
        "SAVE20"               // Parameter {{2}}
      ],
      cards: [
        {
          imageId: imageId,
          buttonText: "products/card1"
        },
        {
          imageId: imageId,
          buttonText: "products/card2"
        }
      ]
    });
    console.log('Carousel message sent successfully:', response.data);
  } catch (error) {
    console.error('Error sending carousel message:', error.response?.data || error.message);
  }
}

async function testSetConversationalCommands() {
  try {
    const response = await axios.post('http://localhost:3000/set-conversational-commands', {
      enableWelcomeMessage: true,
      commands: [
        {
          command_name: "tickets",
          command_description: "Book flight tickets"
        },
        {
          command_name: "hotel",
          command_description: "Book hotel"
        }
      ],
      prompts: ["Book a flight", "plan a vacation"]
    });

    console.log('Conversational commands set successfully:', response.data);
  } catch (error) {
    console.error('Error setting conversational commands:', error.response?.data || error.message);
  }
}

async function testStory() {
  const testPlayerId = 'test_user_123';
  
  console.log('Starting story test...');
  
  try {
    // Test initial scene
    console.log('Loading initial scene...');
    await storyManager.handleScene('intro_welcome', testPlayerId);
    
    // Wait a moment to simulate user interaction
    await new Promise(resolve => setTimeout(resolve, 2000));
    
    // Test making a choice
    console.log('Making choice: choose_nebula');
    await storyManager.handleChoice(testPlayerId, 'choose_nebula');
    
    // Get and display player state
    const playerState = storyManager.getPlayerState(testPlayerId);
    console.log('Final player state:', JSON.stringify(playerState, null, 2));
    
  } catch (error) {
    console.error('Test failed:', error);
  }
}

async function runTests() {
  console.log('Testing WhatsApp Webhook Server...');
  // await testWebhook();
  // const imageId = await testImageUpload();
  // const videoId = await testVideoUpload();
  // await testSendInteractiveButtonWithImageHeader(imageId);
  // await testSendInteractiveList();
  // await testSendVideo(videoId);
  // await testSetConversationalCommands(); these are broken at a facebook level and they have no timeline for them to be resolved
  // await testSendCarousel(imageId); // can only do this if we pay for it 
  await testStory();
}

runTests().catch(console.error);

