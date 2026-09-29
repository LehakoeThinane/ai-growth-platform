param([Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Model)

$ErrorActionPreference = 'Stop'
$apiProject = Join-Path $PSScriptRoot '../src/Api'
$secureKey = Read-Host 'OpenAI API key (input is hidden)' -AsSecureString
$keyPointer = [IntPtr]::Zero
try {
    $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    if ([string]::IsNullOrWhiteSpace($plainKey)) { throw 'An API key is required.' }
    # Use stdin so the key is not placed in process arguments or shell history.
    @{ 'Agents:ApiKey' = $plainKey; 'Agents:Model' = $Model } | ConvertTo-Json -Compress | dotnet user-secrets set --project $apiProject
    if ($LASTEXITCODE -ne 0) { throw 'The .NET user-secrets command failed.' }
    Write-Output 'Model configuration saved in local development user secrets. Model access has not been verified.'
    Write-Output 'Start with: dotnet run --project src/Api --launch-profile http -- --Agents:Mode OpenAI --Agents:CatalogSource Database'
} finally {
    if ($keyPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer) }
    $plainKey = $null
    $secureKey.Dispose()
}
