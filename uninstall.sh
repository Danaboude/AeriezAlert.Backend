#!/bin/bash
#
# AeriezAlert Backend - Uninstall Script
# Version: 1.1
# For: Ubuntu 22.04 LTS
#
# PURPOSE:
# This script safely removes the AeriezAlert backend components.
# It offers multiple levels of uninstallation (Service only, App removal, or Full cleanup).
#
# PROCESS OVERVIEW:
# 1. Stop Backend: Gracefully shuts down the backend service.
# 2. Cleanup Service: Removes the systemd configuration.
# 3. Cleanup Files: Removes application binaries.
# 4. RabbitMQ Cleanup: Removes the dedicated user (while service is still running).
# 5. Stop RabbitMQ: Stops the broker.
# 6. Deep Clean (Optional): Removes RabbitMQ and .NET packages entirely.
#

set -e  # Exit on error for safety

# ==========================================
# CONSTANTS & CONFIGURATION
# ==========================================

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# ==========================================
# HELPER FUNCTIONS
# ==========================================

print_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
print_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
print_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
print_error() { echo -e "${RED}[ERROR]${NC} $1"; }

check_root() {
    if [[ $EUID -eq 0 ]]; then
       print_error "This script should NOT be run as root (don't use sudo)."
       print_info "The script will ask for your password when needed."
       exit 1
    fi
}

print_header() {
    echo ""
    echo "=========================================="
    echo "  AeriezAlert Backend - Uninstall"
    echo "=========================================="
    echo ""
}

# ==========================================
# MAIN EXECUTION
# ==========================================

check_root
print_header

# Request sudo permissions immediately so the script doesn't pause command execution later
print_info "We need sudo permissions to remove system packages and services."
sudo -v

# Keep Sudo Alive: Run a background loop to refresh sudo timestamp
( while true; do sleep 60; sudo -v; done; ) &
SUDO_KEEP_ALIVE_PID=$!

# Ensure we kill the background process when the script exits
trap 'kill $SUDO_KEEP_ALIVE_PID' EXIT

CURRENT_USER=$(whoami)
print_info "Uninstalling for user: $CURRENT_USER"
echo ""

# ==========================================
# INTERACTIVE CONFIRMATION
# ==========================================
echo "This script will remove the AeriezAlert Backend installation."
echo ""
echo "What would you like to do?"
echo "  1) Stop services only (keep everything installed for later)"
echo "  2) Stop services and remove application files (keep RabbitMQ active)"
echo "  3) Complete removal (remove everything including RabbitMQ)"
echo "  4) Cancel"
echo ""

read -p "Enter your choice [1-4]: " UNINSTALL_CHOICE

if [[ $UNINSTALL_CHOICE == "4" ]]; then
    print_info "Uninstall cancelled."
    exit 0
fi

echo ""

# ==========================================
# STEP 1: Stop Backend Service
# ==========================================
# We must stop the running process before deleting any files.
print_info "Step 1: Stopping AeriezAlert Backend service..."

if sudo systemctl is-active --quiet aeriez-backend; then
    sudo systemctl stop aeriez-backend
    print_success "Backend service stopped"
else
    print_warning "Backend service was not running"
fi

# Exit early if the user only wanted to stop services (Choice 1)
if [[ $UNINSTALL_CHOICE == "1" ]]; then
    echo ""
    echo "=========================================="
    print_success "Services Stopped"
    echo "=========================================="
    print_info "Backend service has been stopped."
    print_info "RabbitMQ is still running."
    print_info "Restart with: sudo systemctl start aeriez-backend"
    exit 0
fi

# ==========================================
# STEP 2: Disable and Remove Service
# ==========================================
# We remove the systemd service file so the system no longer tries to start it.
print_info "Step 2: Removing systemd service..."

# Disable auto-start
if sudo systemctl is-enabled --quiet aeriez-backend 2>/dev/null; then
    sudo systemctl disable aeriez-backend
    print_success "Service disabled from auto-start"
fi

# Delete the service definition file
if [[ -f "/etc/systemd/system/aeriez-backend.service" ]]; then
    sudo rm /etc/systemd/system/aeriez-backend.service
    sudo systemctl daemon-reload
    print_success "Service file removed"
else
    print_warning "Service file not found"
fi

# ==========================================
# STEP 3: Remove Application Files
# ==========================================
# Delete the published application folder.
print_info "Step 3: Removing application files..."

if [[ -d "$HOME/aeriez-backend" ]]; then
    rm -rf "$HOME/aeriez-backend"
    print_success "Application files removed from ~/aeriez-backend"
else
    print_warning "Application directory not found"
fi

# Exit early if the user wanted to keep dependencies (Choice 2)
if [[ $UNINSTALL_CHOICE == "2" ]]; then
    echo ""
    echo "=========================================="
    print_success "Application Removed"
    echo "=========================================="
    print_info "Backend service and application have been removed."
    print_info "RabbitMQ is still installed and running."
    print_info "To reinstall, run: ./install.sh"
    exit 0
fi

# ==========================================
# STEP 4: Remove RabbitMQ User
# ==========================================
# We remove the user while RabbitMQ is still running (before we stop/uninstall it).
print_info "Step 4: Removing RabbitMQ user..."

# Check if RabbitMQ is running (it should be)
if sudo systemctl is-active --quiet rabbitmq-server; then
    # Try to delete the user 'aeriez'
    if sudo rabbitmqctl list_users 2>/dev/null | grep -q "aeriez"; then
        sudo rabbitmqctl delete_user aeriez
        print_success "RabbitMQ user 'aeriez' removed"
    else
        print_warning "RabbitMQ user 'aeriez' not found"
    fi
else
    print_warning "RabbitMQ service is not running, skipping user removal."
fi

# ==========================================
# STEP 5: Stop RabbitMQ
# ==========================================
# Now we stop the broker service.
print_info "Step 5: Stopping RabbitMQ service..."

if sudo systemctl is-active --quiet rabbitmq-server; then
    sudo systemctl stop rabbitmq-server
    print_success "RabbitMQ service stopped"
else
    print_warning "RabbitMQ service was not running"
fi


# ==========================================
# STEP 6: Uninstall RabbitMQ (Optional Confirmation)
# ==========================================
print_info "Step 6: Removing RabbitMQ..."

echo ""
# Use the choice from the beginning (Choice 3 implies yes)
# But let's confirm just in case they have other things using RabbitMQ on this server.
read -p "Do you want to completely remove RabbitMQ? (Type 'yes' to confirm): " CONFIRM_RM_MQ
echo ""

if [[ $CONFIRM_RM_MQ == "yes" ]]; then
    # Pass noninteractive frontend directly to sudo to avoid sudoers config issues
    sudo DEBIAN_FRONTEND=noninteractive apt-get remove --purge -y rabbitmq-server
    # Removed autoremove as it can be too aggressive and remove system packages like network-manager
    print_success "RabbitMQ removed"
    
    # Remove data directories to be thorough
    if [[ -d "/var/lib/rabbitmq" ]]; then
        sudo rm -rf /var/lib/rabbitmq
        print_success "RabbitMQ data directory removed"
    fi
    
    if [[ -d "/etc/rabbitmq" ]]; then
        sudo rm -rf /etc/rabbitmq
        print_success "RabbitMQ configuration directory removed"
    fi
else
    print_info "RabbitMQ kept installed (stopped)"
fi

# ==========================================
# STEP 7: Uninstall .NET SDK (Optional)
# ==========================================
print_info "Step 7: Removing .NET SDK..."

echo ""
read -p "Do you want to remove .NET SDK? (Type 'yes' to confirm): " CONFIRM_RM_DOTNET
echo ""

if [[ $CONFIRM_RM_DOTNET == "yes" ]]; then
    # Pass noninteractive frontend directly to sudo to avoid sudoers config issues
    sudo DEBIAN_FRONTEND=noninteractive apt-get remove --purge -y dotnet-sdk-8.0
    # Removed autoremove as it can be too aggressive and remove system packages like network-manager
    print_success ".NET SDK removed"
else
    print_info ".NET SDK kept installed"
fi

# ==========================================
# COMPLETE
# ==========================================
echo ""
echo "=========================================="
print_success "Uninstall Complete!"
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
echo ""
print_info "To reinstall AeriezAlert Backend, run: ./install.sh"
echo ""
