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
docker login $registry -u $username -p $password

# Build the Docker image
docker build -t "$($imageName):$($tag)" .

# Tag the image for the registry
docker tag "$($imageName):$($tag)" "$($registry)/$($imageName):$($tag)"

# Push the image to the registry
docker push "$($registry)/$($imageName):$($tag)"
