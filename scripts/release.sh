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

echo -e "${BLUE}=== Azure Artifacts Credential Provider - Release Script ===${NC}"
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

# Create and push tag
echo ""
echo -e "${YELLOW}Creating tag $NEW_TAG...${NC}"
git tag -a "$NEW_TAG" -m "Release $NEW_TAG"

echo -e "${YELLOW}Pushing tag to origin...${NC}"
git push origin "$NEW_TAG"

echo ""
echo -e "${GREEN}✓ Tag $NEW_TAG created and pushed!${NC}"
echo ""
echo "The GitHub Actions workflow will now:"
echo "  1. Build the credential provider with version $NEW_VERSION"
echo "  2. Create a GitHub release with the artifacts"
echo ""
echo -e "Watch the progress at: ${BLUE}$(git remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')/actions${NC}"
