FROM node:18-alpine

WORKDIR /app

COPY backend/package.json backend/
RUN cd backend && npm install --production

COPY frontend/package.json frontend/
RUN cd frontend && npm install && npm run build

COPY backend/ backend/
COPY frontend/dist frontend/dist/

EXPOSE 3001

CMD ["node", "backend/src/index.js"]
