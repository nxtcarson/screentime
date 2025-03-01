param (
    [Parameter(Mandatory=$true)]
    [string]$version
)

# Validate version format
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Version must be in format X.Y.Z (e.g., 1.0.0)"
    exit 1
}

# Check if git is installed
try {
    git --version | Out-Null
} catch {
    Write-Error "Git is not installed or not in PATH"
    exit 1
}

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Error "Not in a git repository"
    exit 1
}

# Create and push tag
$tagName = "v$version"
Write-Host "Creating tag $tagName..."

try {
    git tag $tagName
    git push origin $tagName
    
    Write-Host "Tag $tagName created and pushed successfully!"
    Write-Host "GitHub Actions workflow should start automatically."
    Write-Host "Check the Actions tab on GitHub to monitor progress."
} catch {
    Write-Error "Failed to create or push tag: $_"
    exit 1
} 