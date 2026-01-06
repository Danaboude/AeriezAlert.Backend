# AeriezAlert Backend (C#)

This is the ASP.NET Core Web API backend for the AeriezAlert application. It handles user authentication (via phone number), MQTT messaging (via CloudAMQP), and simulates a daemon for ticket alerts.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (Optional, for containerized run)

## Local Development

1.  Navigate to the project directory:
    ```bash
    cd backend_csharp/AeriezAlert.Backend
    ```

2.  Run the application:
    ```bash
    dotnet run
    ```
    The server will start at `http://localhost:3001`.

3.  Access the Dashboard:
    Open your browser to `http://localhost:3001/index.html` to control the daemon and send test notifications.

## Deployment to Render

This project includes a `Dockerfile` for easy deployment to [Render](https://render.com/).

### Steps:

1.  **Push your code to GitHub/GitLab.**
2.  **Create a New Web Service on Render:**
    *   Go to your Render Dashboard.
    *   Click **"New +"** -> **"Web Service"**.
    *   Connect your repository.
3.  **Configure the Service:**
    *   **Name:** `aeriez-alert-backend` (or your preferred name)
    *   **Runtime:** `Docker`
    *   **Region:** Select a region close to you.
    *   **Branch:** `main` (or your working branch)
    *   **Context Directory:** `backend_csharp/AeriezAlert.Backend` (IMPORTANT: Ensure this points to the folder containing the `Dockerfile`)
    *   **Docker Command:** Leave blank (Render detects the Dockerfile).
4.  **Environment Variables (Optional):**
    *   If you want to override MQTT credentials in the future, you can add them here (requires code update to read env vars). currently they are hardcoded.
5.  **Create Web Service.**

Render will build the Docker image and deploy it. The app listens on port `8080` internally, which Render detects and exposes via HTTPS.

## API Endpoints

-   `POST /api/auth/login`: authenticate with `{ phoneNumber: "..." }`
-   `GET /api/daemon/status`: Get daemon running status.
-   `POST /api/daemon/start`: Start the polling daemon.
-   `POST /api/daemon/stop`: Stop the polling daemon.
-   `POST /api/notify`: Send a manual MQTT notification.
