#!/bin/bash
#
# AlertAnywhere Backend - Automated Installation Script
# Version: 1.1
# For: Ubuntu 22.04 LTS
#
# PURPOSE:
# This script automates the deployment of the AlertAnywhere backend.
# It handles dependency installation, service configuration, and application deployment.
#
# PROCESS OVERVIEW:
# 1. Verification: Checks for root privileges (must NOT be run as root).
# 2. Configuration: Gathers MQTT details from the user.
# 3. Dependencies: Installs system tools, RabbitMQ, and .NET Runtime.
# 4. Middleware: Configures RabbitMQ with a dedicated user and MQTT plugin.
# 5. Application: Publishes the .NET backend for production.
# 6. Setup: Writes configuration files and systemd service definitions.
# 7. Launch: Starts the service and verifies connectivity.
#

set -e  # Exit script immediately if any command returns a non-zero status (error).

# ==========================================
# CONSTANTS & CONFIGURATION
# ==========================================

# ANSI Color codes for terminal output to make logs readable
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color (reset)

# Default Configuration Values
DEFAULT_MQTT_PORT=1883
DEFAULT_MQTT_USER="aeriez"
DEFAULT_MQTT_PASS="VqcNyxhYjP^3^R5F"

# ==========================================
# HELPER FUNCTIONS
# ==========================================

# Print an informational message (Blue)
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

# Print a success message (Green)
print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

# Print a warning message (Yellow)
print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Print an error message (Red)
print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Ensure the script is NOT run as root.
# We want to run as a normal user so that files are owned by that user,
# avoiding permission issues later. 'sudo' will be invoked only when necessary.
check_root() {
    if [[ $EUID -eq 0 ]]; then
       print_error "This script should NOT be run as root (do not use sudo ./install.sh)."
       print_info "Run it as your standard user. The script will ask for your password when needed."
       exit 1
    fi
}

# Display the installer header
print_header() {
    echo ""
    echo "=========================================="
    echo "  AlertAnywhere Backend - Auto Installer"
    echo "=========================================="
    echo ""
}

# ==========================================
# MAIN EXECUTION
# ==========================================

# 1. Pre-flight Checks
check_root
print_header

# Request sudo permissions immediately so the script doesn't pause later
print_info "We need sudo permissions to install system packages."
sudo -v

# Keep Sudo Alive: Run a background loop to refresh sudo timestamp
# This prevents the script from asking for a password again during long operations
( while true; do sleep 60; sudo -v; done; ) &
SUDO_KEEP_ALIVE_PID=$!

# Ensure we kill the background process when the script exits
trap 'kill $SUDO_KEEP_ALIVE_PID' EXIT

CURRENT_USER=$(whoami)
print_info "Installing for system user: $CURRENT_USER"
echo ""

# ==========================================
# 2. User Configuration (Interactive)
# ==========================================
echo "=========================================="
echo "  MQTT Configuration"
echo "=========================================="
echo "We need to configure how the backend connects to the MQTT broker."
echo ""

print_info "Please enter MQTT Configuration:"

# Loop until we get a valid broker address that is reachable
while true; do
    read -p "Enter MQTT Broker Domain/IP (default: localhost): " INPUT_BROKER_URL
    MQTT_BROKER_URL=${INPUT_BROKER_URL:-localhost}

    # If localhost, we assume it will be installed locally by this script
    if [[ "$MQTT_BROKER_URL" == "localhost" || "$MQTT_BROKER_URL" == "127.0.0.1" ]]; then
        break
    fi

    # For remote addresses, verify connectivity
    print_info "Verifying connectivity to $MQTT_BROKER_URL (Max 10s wait)..."
    
    # 'ping -c 1 -W 10' sends 1 packet with a 10-second timeout
    if ping -c 1 -W 10 "$MQTT_BROKER_URL" > /dev/null 2>&1; then
        print_success "Successfully reached $MQTT_BROKER_URL"
        break
    else
        print_error "Connection FAILED: $MQTT_BROKER_URL did not respond within 10 seconds."
        
        # Give the user a choice: retry or force it
        read -p "Do you want to enter a different address? (Y/n - 'n' forces this IP): " -n 1 -r
        echo ""
        
        if [[ $REPLY =~ ^[Nn]$ ]]; then
            print_warning "Forcing use of unreachable address: $MQTT_BROKER_URL"
            break
        fi
        
        print_info "Please enter the correct address below:"
    fi
done

read -p "Enter MQTT Broker Port (default: $DEFAULT_MQTT_PORT): " INPUT_BROKER_PORT
MQTT_BROKER_PORT=${INPUT_BROKER_PORT:-$DEFAULT_MQTT_PORT}

read -p "Enter MQTT Username (default: $DEFAULT_MQTT_USER): " INPUT_USERNAME
MQTT_USERNAME=${INPUT_USERNAME:-$DEFAULT_MQTT_USER}

read -p "Enter MQTT Password (default: $DEFAULT_MQTT_PASS): " INPUT_PASSWORD
MQTT_PASSWORD=${INPUT_PASSWORD:-$DEFAULT_MQTT_PASS}

print_info "MQTT Broker: $MQTT_BROKER_URL:$MQTT_BROKER_PORT"
print_info "MQTT Username: $MQTT_USERNAME"
echo ""

# ==========================================
# STEP 1: System Update
# ==========================================
# Update the package list and upgrade installed packages to ensure security.
# Install essential tools like curl, git, and unzip.
print_info "Step 1/7: Updating system packages..."

# Export noninteractive frontend to prevent hidden prompts hanging the script
export DEBIAN_FRONTEND=noninteractive

# Avoid using -E which might fail depending on sudoers config. Pass var directly.
# Force IPv4 to prevent potential IPv6 timeouts
sudo DEBIAN_FRONTEND=noninteractive apt-get -o Acquire::ForceIPv4=true update
# Removed full system upgrade (apt-get upgrade) to prevent hangs on kernel updates and speed up installation.
sudo DEBIAN_FRONTEND=noninteractive apt-get -o Acquire::ForceIPv4=true install -y -o Dpkg::Options::="--force-confdef" -o Dpkg::Options::="--force-confold" curl wget git unzip
print_success "System updated successfully"

# ==========================================
# STEP 2: Install RabbitMQ
# ==========================================
# RabbitMQ is the message broker used for MQTT.
# We also install Erlang, which is required by RabbitMQ.
print_info "Step 2/7: Installing RabbitMQ and Erlang..."
sudo DEBIAN_FRONTEND=noninteractive apt-get -o Acquire::ForceIPv4=true install -y -o Dpkg::Options::="--force-confdef" -o Dpkg::Options::="--force-confold" erlang
sudo DEBIAN_FRONTEND=noninteractive apt-get -o Acquire::ForceIPv4=true install -y -o Dpkg::Options::="--force-confdef" -o Dpkg::Options::="--force-confold" rabbitmq-server
print_success "RabbitMQ installed successfully"

# ==========================================
# STEP 3: Configure RabbitMQ
# ==========================================
# - Enable the service to run on background.
# - Enable the MQTT plugin so devices can connect.
# - Create a dedicated user for security.
print_info "Step 3/7: Configuring RabbitMQ service..."
sudo systemctl enable rabbitmq-server
sudo systemctl start rabbitmq-server

# Allow brief pause for service startup
sleep 5

# Enable the MQTT protocol plugin
print_info "Enabling MQTT plugin..."
sudo rabbitmq-plugins enable rabbitmq_mqtt --quiet
sudo rabbitmq-plugins enable rabbitmq_management --quiet

# Restart is required to load the new plugins
sudo systemctl restart rabbitmq-server
sleep 5

print_success "RabbitMQ configured with MQTT support"

# Check if port 1883 (MQTT) is open
if sudo ss -tulpn | grep -q 1883; then
    print_success "MQTT port 1883 is listening"
else
    print_warning "MQTT port 1883 not detected. RabbitMQ may need more time to start."
fi

print_info "Creating RabbitMQ user for MQTT..."

# Cleanup: Delete user if it exists (allows script to be re-run)
sudo rabbitmqctl delete_user $MQTT_USERNAME 2>/dev/null || true

# Create the user and assign administrative rights
sudo rabbitmqctl add_user $MQTT_USERNAME $MQTT_PASSWORD
sudo rabbitmqctl set_user_tags $MQTT_USERNAME administrator
sudo rabbitmqctl set_permissions -p / $MQTT_USERNAME ".*" ".*" ".*"

print_success "RabbitMQ user '$MQTT_USERNAME' created successfully"

# ==========================================
# STEP 4: Install .NET 8.0 SDK
# ==========================================
# The backend is built with .NET 8. We install the SDK to allow building logic.
print_info "Step 4/7: Installing .NET 8.0 SDK..."
sudo DEBIAN_FRONTEND=noninteractive apt-get -o Acquire::ForceIPv4=true install -y -o Dpkg::Options::="--force-confdef" -o Dpkg::Options::="--force-confold" dotnet-sdk-8.0

# Verify the installation worked
DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "not found")
if [[ $DOTNET_VERSION == "not found" ]]; then
    print_error ".NET installation failed"
    exit 1
else
    print_success ".NET $DOTNET_VERSION installed successfully"
fi

# ==========================================
# STEP 5: Deploy Application
# ==========================================
# We compile the C# code into a runnable executable ("Publishing").
print_info "Step 5/7: Deploying AlertAnywhere Backend..."

# Check if this is run from the root or backend_csharp folder
PROJECT_DIR=""

if [[ -d "./AeriezAlert.Backend" ]]; then
    # We are inside backend_csharp
    PROJECT_DIR="."
elif [[ -d "./backend_csharp/AeriezAlert.Backend" ]]; then
    # We are in the parent directory
    PROJECT_DIR="./backend_csharp"
else
    print_error "Backend application source not found in current directory"
    print_info "Please run this script from the project root or the 'backend_csharp' directory"
    exit 1
fi

# Navigate to application directory
cd "$PROJECT_DIR/AeriezAlert.Backend"

# Restore packages
print_info "Restoring NuGet packages..."
dotnet restore -v:q 2>/dev/null || dotnet restore > /dev/null

# Publish application
print_info "Publishing application for production..."
dotnet publish -c Release -o ~/aeriez-backend -v:q 2>/dev/null || dotnet publish -c Release -o ~/aeriez-backend > /dev/null

# Return to original directory
cd - > /dev/null

print_success "Application deployed to ~/aeriez-backend"

# ==========================================
# STEP 6: Configure Application Settings
# ==========================================
# We create the appsettings.json file with the MQTT credentials we gathered earlier.
print_info "Step 6/7: Configuring application settings..."

cat > ~/aeriez-backend/appsettings.json <<EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "MqttSettingsBrokerUrl": "$MQTT_BROKER_URL",
  "MqttSettingsBrokerPort": $MQTT_BROKER_PORT,
  "MqttSettingsUsername": "$MQTT_USERNAME",
  "MqttSettingsPassword": "$MQTT_PASSWORD"
}
EOF

if [[ $MQTT_BROKER_URL == "localhost" ]]; then
    print_success "Application configured for localhost RabbitMQ"
else
    print_success "Application configured for remote MQTT broker: $MQTT_BROKER_URL:$MQTT_BROKER_PORT"
fi

# ==========================================
# STEP 7: Create Auto-Start Service (systemd)
# ==========================================
# We create a linux service so the app starts automatically on reboot
# and restarts automatically if it crashes.
print_info "Step 7/7: Creating systemd service for auto-start..."

# Write the service definition file
sudo bash -c "cat > /etc/systemd/system/aeriez-backend.service" <<EOF
[Unit]
Description=AlertAnywhere Backend Service
After=network.target rabbitmq-server.service
Requires=rabbitmq-server.service

[Service]
Type=simple
# The directory where the app lives
WorkingDirectory=/home/$CURRENT_USER/aeriez-backend
# The command to start the app
ExecStart=/usr/bin/dotnet /home/$CURRENT_USER/aeriez-backend/AeriezAlert.Backend.dll
# Restart policy: Always restart unless explicitly stopped
Restart=always
RestartSec=10
SyslogIdentifier=aeriez-backend
User=$CURRENT_USER
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
EOF

# Reload systemd to recognize the new file
sudo systemctl daemon-reload

# Enable the service (start on boot)
sudo systemctl enable aeriez-backend.service

# Start the service immediately
sudo systemctl start aeriez-backend.service || true

# Give it a moment to initialize
sleep 3

print_success "Systemd service created and started"

# ==========================================
# Post-Installation Checks
# ==========================================

echo ""
echo "=========================================="
print_success "Installation Complete!"
echo "=========================================="
echo ""
echo "========================================================"
echo "              DEVELOPED BY AERIEZ TEAM"
echo "========================================================"
echo ""
echo "                    /\\\\"
echo "                   //  \\\\"
echo "                  //    \\\\"
echo "                 //      \\\\"
echo "                //   /\\\\   \\\\"
echo "               //   //  \\\\   \\\\"
echo "              //   //    \\\\   \\\\"
echo "             //   //      \\\\   \\\\"
echo "            //   //        \\\\   \\\\"
echo "           //___//__________\\\\___\\\\"
echo ""
echo "              A L E R T   A N Y W H E R E"
echo ""
echo "========================================================"
echo ""

# Check if the service is actually running
if sudo systemctl is-active --quiet aeriez-backend; then
    print_success "AlertAnywhere Backend service is ACTIVE"
    
    # Check logs for successful MQTT connection
    sleep 2
    if sudo journalctl -u aeriez-backend -n 20 | grep -q "Connected to MQTT Broker"; then
        print_success "Verification: Successfully connected to MQTT Broker"
    else
        print_warning "Verification: Service running, but MQTT connection log not yet found."
        print_info "This might just be a delay. Check logs with: sudo journalctl -u aeriez-backend -f"
    fi
else
    print_error "Service is NOT running. Check errors with: sudo systemctl status aeriez-backend"
fi

echo ""
echo "Useful Commands:"
echo "  Check service status:  sudo systemctl status aeriez-backend"
echo "  View live logs:        sudo journalctl -u aeriez-backend -f"
echo "  Restart service:       sudo systemctl restart aeriez-backend"
echo "  Stop service:          sudo systemctl stop aeriez-backend"
echo ""
echo "Configuration file: ~/aeriez-backend/appsettings.json"
echo ""
print_info "The application will automatically start on system reboot"
