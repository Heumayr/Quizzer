# Legt ein selbst erzeugtes Zertifikat fuer den Betrieb auf DIESEM Rechner an (ohne Tailscale):
# certs\server.crt und certs\server.key fuer Caddy, dazu certs\server.sha256 mit dem
# Fingerabdruck, den das WPF unter Einstellungen -> Server braucht.
#   .\zertifikat-lokal.ps1           legt an, wenn noch keines da ist
#   .\zertifikat-lokal.ps1 -Neu      ersetzt ein vorhandenes - der Fingerabdruck im WPF aendert sich!
# Braucht PowerShell 7 (PEM-Ausgabe gibt es erst ab .NET 7).
param([switch]$Neu, [int]$Jahre = 5)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Bitte mit PowerShell 7 starten (pwsh).' }

$crt = 'certs/server.crt'
$key = 'certs/server.key'
$sha = 'certs/server.sha256'

if ((Test-Path $crt) -and -not $Neu) {
    Write-Host "Es gibt schon ein Zertifikat - nichts geaendert. Fingerabdruck: $(Get-Content $sha -ErrorAction SilentlyContinue)"
    exit 0
}

New-Item -ItemType Directory -Force certs | Out-Null

$ecdsa = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
$request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
    'CN=localhost', $ecdsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256)

# Gilt fuer localhost und den Rechnernamen. Ein anderer Name stoert das WPF nicht - es nimmt ein
# selbst erzeugtes Zertifikat ohnehin nur ueber den Fingerabdruck an (QuizzerApiClient).
$names = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
$names.AddDnsName('localhost')
$names.AddDnsName($env:COMPUTERNAME.ToLowerInvariant())
$names.AddIpAddress([System.Net.IPAddress]::Loopback)
$request.CertificateExtensions.Add($names.Build())
$request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
$request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
    [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true))
$serverAuth = [System.Security.Cryptography.OidCollection]::new()
$serverAuth.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1')) | Out-Null
$request.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($serverAuth, $false))

$now = [DateTimeOffset]::UtcNow
$certificate = $request.CreateSelfSigned($now.AddMinutes(-5), $now.AddYears($Jahre))

# Derselbe Fingerabdruck, den QuizzerApiClient.Thumbprint rechnet: SHA-256 ueber die DER-Form.
$fingerprint = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($certificate.RawData))

Set-Content $crt $certificate.ExportCertificatePem() -NoNewline -Encoding ascii
Set-Content $key $ecdsa.ExportPkcs8PrivateKeyPem() -NoNewline -Encoding ascii
Set-Content $sha $fingerprint -NoNewline -Encoding ascii

# Den Schluessel darf nur der eigene Benutzer lesen.
$acl = Get-Acl $key
$acl.SetAccessRuleProtection($true, $false)
$acl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($env:USERNAME, 'FullControl', 'Allow'))
Set-Acl $key $acl

Write-Host "Zertifikat angelegt, gueltig bis $($certificate.NotAfter.ToString('yyyy-MM-dd'))."
Write-Host "Fingerabdruck fuer das WPF (Einstellungen -> Server): $fingerprint"
if ($Neu) { Write-Host 'Caddy neu anlegen: docker compose up -d --force-recreate caddy - und den Fingerabdruck im WPF ersetzen.' }
