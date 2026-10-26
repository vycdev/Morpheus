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
        [string]$DockerfilePath,
        [string]$LicenseFile
    )

    $localImage = "$($ImageName):$($Tag)"
    $registryImage = "$($Registry)/$($ImageName):$($Tag)"

    echo ""
    echo "Building image: $localImage"
    docker build --secret "id=sixlabors_license,src=$LicenseFile" -f $DockerfilePath -t $localImage $ContextPath
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
$tag = Get-RequiredValue "IMAGE_TAG" $IMAGE_TAG
$dockerfile = Join-Path $repoRoot "Dockerfile"

# The license is mounted only for compilation, never copied into an image layer.
$licenseFile = $SIXLABORS_LICENSE_FILE
if ([string]::IsNullOrWhiteSpace($licenseFile)) {
    $licenseFile = Join-Path $repoRoot "sixlabors.lic"
}
elseif (-not [System.IO.Path]::IsPathRooted($licenseFile)) {
    $licenseFile = Join-Path $repoRoot $licenseFile
}
if (-not (Test-Path -LiteralPath $licenseFile -PathType Leaf)) {
    throw "Missing Six Labors license. Add sixlabors.lic at the repository root or set SIXLABORS_LICENSE_FILE in publish/.env."
}
$licenseFile = (Resolve-Path -LiteralPath $licenseFile).Path

# Echo variables
echo "Registry: $($registry)"
echo "Username: $($username)"
echo "Image Name: $($imageName)"
echo "Tag: $($tag)"

# Login to the Docker registry
$password | docker login $registry -u $username --password-stdin
Assert-DockerSucceeded "Docker login"

# Build and publish the runtime image
Publish-DockerImage `
    -Registry $registry `
    -ImageName $imageName `
    -Tag $tag `
    -ContextPath $repoRoot `
    -DockerfilePath $dockerfile `
    -LicenseFile $licenseFile
