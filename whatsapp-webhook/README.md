# Spider Bubble Whatsapp Webhook

## System Dependencies

```bash
sudo apt-get update
sudo npm install -g node-gyp
sudo apt-get install python3 build-essential sqlite3 libsqlite3-dev
npm install sqlite3 --build-from-source
```

## Install Node.js

```bash
curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash -
sudo apt-get install -y nodejs
```

## Install Ngrok

```bash
brew install ngrok


```bash
ngrok config edit
```


Add the following to the file:


```bash
version: 3
agent:
  authtoken: [AUTH_TOKEN]
endpoints:
  - name: whatsapp-server
    url: separately-noble-urchin.ngrok-free.app
    upstream:
      url: 3000
```

## Install Ngrok Auth Token

```bash
ngrok config add-authtoken [YOUR_AUTH_TOKEN]
```

## Run the server

```bash
node server.js
```

## Run ngrok

```bash
ngrok start --all
```
