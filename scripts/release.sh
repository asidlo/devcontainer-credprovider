#!/bin/bash
# Interactive release script for creating version tags
#
# Usage: ./scripts/release.sh [major|minor|patch|VERSION]
#
# Examples:
#   ./scripts/release.sh           # Interactive mode - prompts for version bump
#   ./scripts/release.sh patch     # Auto-increment patch version
#   ./scripts/release.sh minor     # Auto-increment minor version  
#   ./scripts/release.sh major     # Auto-increment major version
#   ./scripts/release.sh 2.0.0     # Set specific version

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$REPO_ROOT"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Detect OS for package manager
detect_os() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        echo "$ID"
    elif [ -f /etc/mariner-release ]; then
        echo "mariner"
    else
        echo "unknown"
    fi
}

# Install GPG based on OS
install_gpg() {
    local os="$1"
    echo -e "${YELLOW}Installing GPG...${NC}"
    
    case "$os" in
        ubuntu|debian)
            sudo apt-get update && sudo apt-get install -y gnupg
            ;;
        mariner|azurelinux)
            sudo tdnf install -y gnupg2
            ;;
        fedora|rhel|centos)
            sudo dnf install -y gnupg2 || sudo yum install -y gnupg2
            ;;
        alpine)
            sudo apk add gnupg
            ;;
        *)
            echo -e "${RED}Unknown OS: $os. Please install GPG manually.${NC}"
            return 1
            ;;
    esac
    
    echo -e "${GREEN}✓ GPG installed${NC}"
}

# Check and setup GPG
check_gpg_setup() {
    local os
    os=$(detect_os)
    
    # Check if GPG is installed
    if ! command -v gpg &>/dev/null; then
        echo -e "${YELLOW}GPG is not installed.${NC}"
        read -p "Install GPG? [Y/n]: " INSTALL_GPG
        if [[ ! "$INSTALL_GPG" =~ ^[Nn]$ ]]; then
            install_gpg "$os" || return 1
        else
            echo -e "${YELLOW}Skipping GPG installation. Tags will be unsigned.${NC}"
            return 0
        fi
    fi
    
    # Check if GPG keys exist
    if ! gpg --list-secret-keys --keyid-format LONG 2>/dev/null | grep -q "sec"; then
        echo -e "${YELLOW}No GPG keys found.${NC}"
        echo ""
        echo "To create a GPG key, run:"
        echo -e "  ${BLUE}gpg --full-generate-key${NC}"
        echo ""
        echo "Recommended settings:"
        echo "  - Key type: RSA and RSA (default)"
        echo "  - Key size: 4096"
        echo "  - Expiration: 0 (does not expire) or 1y"
        echo "  - Use your GitHub email address"
        echo ""
        read -p "Continue without GPG signing? [y/N]: " CONTINUE_NO_SIGN
        if [[ ! "$CONTINUE_NO_SIGN" =~ ^[Yy]$ ]]; then
            echo -e "${YELLOW}Aborted. Create a GPG key and try again.${NC}"
            exit 0
        fi
        return 0
    fi
    
    # Check if git is configured with GPG
    local signing_key
    signing_key=$(git config --get user.signingkey 2>/dev/null || echo "")
    
    if [ -z "$signing_key" ]; then
        echo -e "${YELLOW}Git is not configured with a GPG signing key.${NC}"
        echo ""
        echo "Available GPG keys:"
        gpg --list-secret-keys --keyid-format LONG 2>/dev/null | grep -E "^sec|^uid" | head -10
        echo ""
        
        # Try to auto-detect the key
        local detected_key
        detected_key=$(gpg --list-secret-keys --keyid-format LONG 2>/dev/null | grep "^sec" | head -1 | awk -F'/' '{print $2}' | awk '{print $1}')
        
        if [ -n "$detected_key" ]; then
            read -p "Configure git to use key $detected_key? [Y/n]: " USE_KEY
            if [[ ! "$USE_KEY" =~ ^[Nn]$ ]]; then
                git config --global user.signingkey "$detected_key"
                git config --global tag.gpgSign true
                git config --global commit.gpgSign true
                echo -e "${GREEN}✓ Git configured to use GPG key $detected_key${NC}"
            fi
        else
            echo "To configure manually, run:"
            echo -e "  ${BLUE}git config --global user.signingkey <KEY_ID>${NC}"
            echo -e "  ${BLUE}git config --global tag.gpgSign true${NC}"
        fi
    else
        # Verify the configured key exists
        if ! gpg --list-secret-keys --keyid-format LONG 2>/dev/null | grep -q "$signing_key"; then
            echo -e "${RED}Warning: Configured signing key $signing_key not found in GPG keyring!${NC}"
            echo "Available keys:"
            gpg --list-secret-keys --keyid-format LONG 2>/dev/null | grep -E "^sec|^uid" | head -10
            return 1
        fi
        echo -e "${GREEN}✓ GPG signing configured (key: $signing_key)${NC}"
    fi
    
    # Ensure tag.gpgSign is enabled
    local tag_sign
    tag_sign=$(git config --get tag.gpgsign 2>/dev/null || echo "")
    if [ "$tag_sign" != "true" ]; then
        git config --global tag.gpgSign true
        echo -e "${GREEN}✓ Enabled tag.gpgSign${NC}"
    fi
    
    return 0
}

echo -e "${BLUE}=== Azure Artifacts Credential Provider - Release Script ===${NC}"
echo ""

# Check GPG setup
echo -e "${YELLOW}Checking GPG configuration...${NC}"
check_gpg_setup
echo ""

# Ensure we have the latest tags
echo -e "${YELLOW}Fetching latest tags...${NC}"
git fetch --tags --quiet

# Get current version from latest tag
LATEST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "v0.0.0")
CURRENT_VERSION="${LATEST_TAG#v}"

echo -e "Current version: ${GREEN}$LATEST_TAG${NC}"
echo ""

# Parse current version
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"
MAJOR=${MAJOR:-0}
MINOR=${MINOR:-0}
PATCH=${PATCH:-0}

# Calculate next versions
NEXT_PATCH="$MAJOR.$MINOR.$((PATCH + 1))"
NEXT_MINOR="$MAJOR.$((MINOR + 1)).0"
NEXT_MAJOR="$((MAJOR + 1)).0.0"

# Determine version from argument or prompt
if [ -n "$1" ]; then
    case "$1" in
        major)
            NEW_VERSION="$NEXT_MAJOR"
            ;;
        minor)
            NEW_VERSION="$NEXT_MINOR"
            ;;
        patch)
            NEW_VERSION="$NEXT_PATCH"
            ;;
        *)
            # Assume it's a specific version number
            NEW_VERSION="${1#v}"  # Remove 'v' prefix if present
            ;;
    esac
else
    # Interactive mode
    echo "Select version bump:"
    echo -e "  ${GREEN}1)${NC} Patch  → v$NEXT_PATCH  (bug fixes, no breaking changes)"
    echo -e "  ${GREEN}2)${NC} Minor  → v$NEXT_MINOR  (new features, backwards compatible)"
    echo -e "  ${GREEN}3)${NC} Major  → v$NEXT_MAJOR  (breaking changes)"
    echo -e "  ${GREEN}4)${NC} Custom version"
    echo ""
    read -p "Enter choice [1-4]: " CHOICE

    case "$CHOICE" in
        1)
            NEW_VERSION="$NEXT_PATCH"
            ;;
        2)
            NEW_VERSION="$NEXT_MINOR"
            ;;
        3)
            NEW_VERSION="$NEXT_MAJOR"
            ;;
        4)
            read -p "Enter version (e.g., 2.0.0): " CUSTOM_VERSION
            NEW_VERSION="${CUSTOM_VERSION#v}"
            ;;
        *)
            echo -e "${RED}Invalid choice. Exiting.${NC}"
            exit 1
            ;;
    esac
fi

NEW_TAG="v$NEW_VERSION"

# Validate version format
if ! [[ "$NEW_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?$ ]]; then
    echo -e "${RED}Error: Invalid version format '$NEW_VERSION'${NC}"
    echo "Expected format: MAJOR.MINOR.PATCH (e.g., 1.2.3) or MAJOR.MINOR.PATCH-prerelease (e.g., 1.2.3-beta.1)"
    exit 1
fi

# Check if tag already exists
if git rev-parse "$NEW_TAG" >/dev/null 2>&1; then
    echo -e "${RED}Error: Tag $NEW_TAG already exists!${NC}"
    exit 1
fi

echo ""
echo -e "New version: ${GREEN}$NEW_TAG${NC}"
echo ""

# Show what will be released
echo -e "${YELLOW}Changes since $LATEST_TAG:${NC}"
if [ "$LATEST_TAG" != "v0.0.0" ]; then
    git log --oneline "$LATEST_TAG"..HEAD | head -20
    COMMIT_COUNT=$(git rev-list --count "$LATEST_TAG"..HEAD)
    if [ "$COMMIT_COUNT" -gt 20 ]; then
        echo "  ... and $((COMMIT_COUNT - 20)) more commits"
    fi
else
    git log --oneline | head -20
fi
echo ""

# Confirm
read -p "Create tag $NEW_TAG and push to trigger release? [y/N]: " CONFIRM
if [[ ! "$CONFIRM" =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Aborted.${NC}"
    exit 0
fi

# Set GPG_TTY for passphrase prompts
export GPG_TTY=$(tty)

# Create tag (signed if possible, fail if signing is configured but fails)
echo ""
echo -e "${YELLOW}Creating tag $NEW_TAG...${NC}"

# Check if signing is configured
SIGNING_KEY=$(git config --get user.signingkey 2>/dev/null || echo "")
TAG_SIGN=$(git config --get tag.gpgsign 2>/dev/null || echo "")

if [ -n "$SIGNING_KEY" ] || [ "$TAG_SIGN" = "true" ]; then
    echo -e "${YELLOW}GPG signing key detected, creating signed tag...${NC}"
    if git tag -s "$NEW_TAG" -m "Release $NEW_TAG"; then
        echo -e "${GREEN}✓ Created signed tag${NC}"
    else
        echo -e "${RED}✗ Failed to create signed tag!${NC}"
        echo ""
        echo "Troubleshooting tips:"
        echo "  1. Ensure GPG_TTY is set: export GPG_TTY=\$(tty)"
        echo "  2. Test GPG: echo 'test' | gpg --clearsign"
        echo "  3. Check key: gpg --list-secret-keys --keyid-format LONG"
        echo "  4. Verify git config: git config --get user.signingkey"
        echo ""
        echo -e "${RED}Tag NOT created. Fix GPG signing and try again.${NC}"
        exit 1
    fi
else
    echo -e "${YELLOW}Note: No signing key configured; creating unsigned annotated tag.${NC}"
    echo -e "${YELLOW}Tip: Configure tag signing with:${NC}"
    echo "  git config --global user.signingkey <KEY_ID>"
    echo "  git config --global tag.gpgSign true"
    git tag -a "$NEW_TAG" -m "Release $NEW_TAG"
fi

echo -e "${YELLOW}Pushing tag to origin...${NC}"
if ! git push origin "$NEW_TAG"; then
    echo -e "${RED}✗ Failed to push tag!${NC}"
    echo "Removing local tag..."
    git tag -d "$NEW_TAG"
    exit 1
fi

echo ""
echo -e "${GREEN}✓ Tag $NEW_TAG created and pushed!${NC}"
echo ""
echo "The GitHub Actions workflow will now:"
echo "  1. Build the credential provider with version $NEW_VERSION"
echo "  2. Create a GitHub release with the artifacts"
echo ""
echo -e "Watch the progress at: ${BLUE}$(git remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')/actions${NC}"
