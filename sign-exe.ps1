<#
.SYNOPSIS
  Self-signs publish\ShoppingCartApp.exe with a local code-signing certificate.

.IMPORTANT - what this does and does not do
  This makes the exe's Digital Signatures tab show "Ethan Brown" as the signer, and (because
  the certificate is also added to this Windows account's Trusted Root/Trusted Publisher
  stores) makes Authenticode validation on THIS machine, under THIS user account, come back
  clean instead of "untrusted root".

  It does NOT satisfy Smart App Control. SAC's reputation check requires a certificate chained
  to a Certificate Authority Microsoft's ecosystem already trusts (a paid, identity-verified
  cert from a CA like DigiCert/Sectigo) - a self-signed cert has no such chain, so SAC still
  treats the file as unknown. Signing it does not remove the need for a dotnet-hosted launch
  path if you need to actually run the unsigned exe on this machine.

  Trust added here is also scoped to Cert:\CurrentUser - it only affects this Windows account
  on this machine, not any other machine or user the exe might be copied to.

.NOTES
  Run dotnet publish (see ShoppingCartAppVerifier's README.md for the exact command) before
  this - it signs publish\ShoppingCartApp.exe.

  Reuses the same "CN=Ethan Brown" signing certificate created for ShoppingCartAppVerifier
  (same subject + friendly name), rather than minting a second one - one cert covers every exe
  built by this Windows account.
#>

$ErrorActionPreference = "Stop"

$exePath = Join-Path $PSScriptRoot "publish\ShoppingCartApp.exe"
if (-not (Test-Path $exePath)) {
    throw "Not found: $exePath - run 'dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish' first."
}

$subject = "CN=Ethan Brown"
$friendlyName = "ShoppingCartAppVerifier local dev signing (self-signed - not trusted by Smart App Control)"

# --- Find (or create) the signing certificate ---
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Subject -eq $subject -and $_.FriendlyName -eq $friendlyName } |
    Sort-Object NotAfter -Descending | Select-Object -First 1

if (-not $cert) {
    Write-Output "No existing signing certificate found - creating one..."
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $subject `
        -FriendlyName $friendlyName `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyUsage DigitalSignature `
        -KeyExportPolicy Exportable `
        -NotAfter (Get-Date).AddYears(5)
    Write-Output "Created certificate: $($cert.Thumbprint)"
} else {
    Write-Output "Reusing existing certificate: $($cert.Thumbprint)"
}

# --- Trust it locally (CurrentUser only) so Authenticode validation on this machine is clean ---
foreach ($store in @("Root", "TrustedPublisher")) {
    $storeObj = New-Object System.Security.Cryptography.X509Certificates.X509Store($store, "CurrentUser")
    $storeObj.Open("ReadWrite")
    if (-not ($storeObj.Certificates | Where-Object { $_.Thumbprint -eq $cert.Thumbprint })) {
        $storeObj.Add($cert)
        Write-Output "Added to CurrentUser\$store"
    }
    $storeObj.Close()
}

# --- Find signtool.exe (Windows SDK) ---
$signtool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\x64\\" } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $signtool) {
    throw "signtool.exe not found under Windows Kits - install the Windows SDK (Windows App Certification Kit component) to get it."
}

# --- Sign ---
& $signtool sign /sha1 $cert.Thumbprint /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 $exePath
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed with exit code $LASTEXITCODE" }

# --- Verify ---
$sig = Get-AuthenticodeSignature $exePath
Write-Output ""
Write-Output "Status: $($sig.Status)"
Write-Output "Signer: $($sig.SignerCertificate.Subject)"
Write-Output ""
Write-Output "Note: SignerCertificate trust is local to this Windows account. Smart App Control"
Write-Output "still treats this file as unknown."
