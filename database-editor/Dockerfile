FROM node:22-alpine@sha256:0a7108bf6c7bf5de370ffb1a3ed6be93d405b43ff159f681a8d18c0e2bc2e402
WORKDIR /app
COPY database-editor/package.json database-editor/package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts --no-audit --no-fund && npm cache clean --force
COPY database-editor/server ./server
COPY database-editor/client ./client
COPY Assets/FootballSimulator/Data/FootballWorld/Schemas/*.json ./schemas/
USER node
EXPOSE 3000
CMD ["node", "server/index.mjs"]
