const axios = require('axios');
const fs = require('fs');
require('dotenv').config();

async function initiateUploadSession(fileName, fileLength, fileType) {
  try {
    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${process.env.APP_ID}/uploads`,
      null,
      {
        params: {
          file_length: fileLength,
          file_type: fileType,
          file_name: fileName,
          access_token: process.env.WHATSAPP_BUSINESS_MANAGEMENT_ACCESS_TOKEN
        }
      }
    );
    return response.data.id; // Returns "upload:<UPLOAD_SESSION_ID>"
  } catch (error) {
    console.error('Error initiating upload:', error.response?.data || error.message);
    throw error;
  }
}

async function uploadFile(sessionId, filePath) {
  try {
    const fileData = fs.readFileSync(filePath);
    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${sessionId}`,
      fileData,
      {
        headers: {
          'Authorization': `OAuth ${process.env.WHATSAPP_BUSINESS_MANAGEMENT_ACCESS_TOKEN}`,
          'file_offset': '0',
          'Content-Type': 'application/octet-stream'
        }
      }
    );
    return response.data.h; // Returns the file handle
  } catch (error) {
    console.error('Error uploading file:', error.response?.data || error.message);
    throw error;
  }
}

async function createCarouselTemplate() {
  try {
    // Upload first image
    const file1Path = './testing/assets/image.png';
    const file1Stats = fs.statSync(file1Path);
    const session1Id = await initiateUploadSession(
      'product1.png',
      file1Stats.size,
      'image/png'
    );
    const mediaHandle1 = await uploadFile(session1Id, file1Path);

    // Upload second image
    const file2Path = './testing/assets/image.png';
    const file2Stats = fs.statSync(file2Path);
    const session2Id = await initiateUploadSession(
      'product2.png',
      file2Stats.size,
      'image/png'
    );
    const mediaHandle2 = await uploadFile(session2Id, file2Path);

    console.log('Media handles:', { mediaHandle1, mediaHandle2 });

    // Save media handles to a JSON file for later use
    fs.writeFileSync('./media-handles.json', JSON.stringify({
      mediaHandle1,
      mediaHandle2,
      timestamp: new Date().toISOString()
    }, null, 2));

    const templateData = {
      name: "product_carousel_template",
      language: "en_US",
      category: "marketing",
      components: [
        {
          type: "body",
          text: "Check out our featured products! {{1}} Use code {{2}} for a special discount.",
          example: {
            body_text: [
              [
                "Limited time offer!",
                "SAVE20"
              ]
            ]
          }
        },
        {
          type: "carousel",
          cards: [
            {
              components: [
                {
                  type: "header",
                  format: "image",
                  example: {
                    header_handle: [
                      mediaHandle1
                    ]
                  }
                },
                {
                  type: "body",
                  text: "Premium Product 1 - Exclusive Design"
                },
                {
                  type: "buttons",
                  buttons: [
                    {
                      type: "url",
                      text: "Shop Now",
                      url: "https://yourdomain.com/products/{{1}}",
                      example: [
                        "product1"
                      ]
                    }
                  ]
                }
              ]
            },
            {
              components: [
                {
                  type: "header",
                  format: "image",
                  example: {
                    header_handle: [
                      mediaHandle2
                    ]
                  }
                },
                {
                  type: "body",
                  text: "Premium Product 2 - Limited Edition"
                },
                {
                  type: "buttons",
                  buttons: [
                    {
                      type: "url",
                      text: "Shop Now",
                      url: "https://yourdomain.com/products/{{1}}",
                      example: [
                        "product2"
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
      ]
    };

    const response = await axios.post(
      `https://graph.facebook.com/v21.0/${process.env.WHATSAPP_BUSINESS_ACCOUNT_ID}/message_templates`,
      templateData,
      {
        headers: {
          'Authorization': `Bearer ${process.env.WHATSAPP_BUSINESS_MANAGEMENT_ACCESS_TOKEN}`,
          'Content-Type': 'application/json'
        }
      }
    );

    console.log('Template created successfully:', response.data);
  } catch (error) {
    if (error.response) {
      console.error('Error creating template:', error.response.data);
      console.error('Status:', error.response.status);
      console.error('Headers:', error.response.headers);
    } else {
      console.error('Error creating template:', error.message);
    }
  }
}

createCarouselTemplate(); 