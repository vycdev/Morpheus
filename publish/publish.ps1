$ErrorActionPreference = "Stop"

function Get-RequiredValue {
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Missing required publish setting: $Name."
    }

    return $Value.Trim()
}

function Assert-DockerSucceeded {
    param([string]$Step)

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

function Publish-DockerImage {
    param(
        [string]$Registry,
        [string]$ImageName,
        [string]$Tag,
        [string]$ContextPath,
        [string]$DockerfilePath
    )

    $localImage = "$($ImageName):$($Tag)"
    $registryImage = "$($Registry)/$($ImageName):$($Tag)"

    echo ""
    echo "Building image: $localImage"
    docker build -f $DockerfilePath -t $localImage $ContextPath
    Assert-DockerSucceeded "Docker build for $ImageName"

    echo "Tagging image: $registryImage"
    docker tag $localImage $registryImage
    Assert-DockerSucceeded "Docker tag for $ImageName"

    echo "Pushing image: $registryImage"
    docker push $registryImage
    Assert-DockerSucceeded "Docker push for $ImageName"
}

# Get the directory of the script
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir
$envPath = Join-Path $scriptDir ".env"

# Load .env file
$envFile = Get-Content $envPath | ForEach-Object {
    $line = $_.Trim()
    if (-not [string]::IsNullOrWhiteSpace($line) -and -not $line.StartsWith("#")) {
        $name, $value = $line -split '=', 2
        Set-Variable -Name $name -Value $value
    }
}

# Define variables from .env
$registry = Get-RequiredValue "REGISTRY" $REGISTRY
$username = Get-RequiredValue "USERNAME" $USERNAME
$password = Get-RequiredValue "PASSWORD" $PASSWORD
$imageName = Get-RequiredValue "IMAGE_NAME" $IMAGE_NAME
$dashboardImageName = if ([string]::IsNullOrWhiteSpace($DASHBOARD_IMAGE_NAME)) { "$($imageName)-dashboard" } else { $DASHBOARD_IMAGE_NAME.Trim() }
$tag = Get-RequiredValue "IMAGE_TAG" $IMAGE_TAG
$apiDockerfile = Join-Path $repoRoot "Dockerfile"
$dashboardContext = Join-Path $repoRoot "DashboardWeb"
$dashboardDockerfile = Join-Path $dashboardContext "Dockerfile"

# Echo variables
echo "Registry: $($registry)"
echo "Username: $($username)"
echo "API Image Name: $($imageName)"
echo "Dashboard Image Name: $($dashboardImageName)"
echo "Tag: $($tag)"

# Login to the Docker registry
$password | docker login $registry -u $username --password-stdin
Assert-DockerSucceeded "Docker login"

# Build and publish both runtime images
Publish-DockerImage `
    -Registry $registry `
    -ImageName $imageName `
    -Tag $tag `
    -ContextPath $repoRoot `
    -DockerfilePath $apiDockerfile

Publish-DockerImage `
    -Registry $registry `
    -ImageName $dashboardImageName `
    -Tag $tag `
    -ContextPath $dashboardContext `
    -DockerfilePath $dashboardDockerfile
