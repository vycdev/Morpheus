$ErrorActionPreference = "Stop"

function Assert-DockerSucceeded {
    param([string]$Step)

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

# Get the directory of the script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$envPath = Join-Path $scriptDir ".env"

# Load .env file
$envFile = Get-Content $envPath | ForEach-Object {
    $name, $value = $_ -split '=', 2
    Set-Variable -Name $name -Value $value
}

# Define variables from .env
$registry = $REGISTRY
$username = $USERNAME
$password = $PASSWORD
$imageName = $IMAGE_NAME
$tag = $IMAGE_TAG

# Echo variables
echo "Registry: $($registry)"
echo "Username: $($username)"
echo "Image Name: $($imageName)"
echo "Tag: $($tag)"

# Login to the Docker registry
$password | docker login $registry -u $username --password-stdin
Assert-DockerSucceeded "Docker login"

# Build the Docker image
docker build -t "$($imageName):$($tag)" .
Assert-DockerSucceeded "Docker build"

# Tag the image for the registry
docker tag "$($imageName):$($tag)" "$($registry)/$($imageName):$($tag)"
Assert-DockerSucceeded "Docker tag"

# Push the image to the registry
docker push "$($registry)/$($imageName):$($tag)"
Assert-DockerSucceeded "Docker push"
