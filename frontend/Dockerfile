# Stage 1: Build the React application
FROM node:18-alpine AS build-stage

WORKDIR /app

# Install dependencies first (better layer caching).
COPY package*.json ./
RUN npm install

# Copy the rest of the application.
COPY . .

# Build with the "docker" mode so Vite loads .env.docker (localhost service URLs
# for the browser). .env.production is left untouched for the Render deploy.
RUN npm run build -- --mode docker

# Stage 2: Serve the static build with Nginx
FROM nginx:stable-alpine AS production-stage

# SPA routing config (try_files fallback to index.html).
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy the build output to Nginx's html directory.
COPY --from=build-stage /app/dist /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
