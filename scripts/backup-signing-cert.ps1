<#
.SYNOPSIS
    Exports the SAP Logon extension's code-signing certificate (with private key) to a password-protected .pfx.
.DESCRIPTION
    Prompts for the password so it never appears in the command line, shell history or logs.
    Keep the password somewhere other than the .pfx (e.g. a password manager).
    Restore on a new machine with:
        Import-PfxCertificate -FilePath <file.pfx> -CertStoreLocation Cert:\CurrentUser\My -Password (Read-Host -AsSecureString)
    pack.ps1 -Sign then finds and reuses the restored CN=SapLogonDev certificate.
.EXAMPLE
    .\scripts\backup-signing-cert.ps1 -Destination D:\Backup
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Destination,
    [string] $Thumbprint = '5F2CEBF0E9423117982D2D991080FFDD28AE301D'
)

$ErrorActionPreference = 'Stop'
$cert = Get-Item "Cert:\CurrentUser\My\$Thumbprint"
if (-not $cert.HasPrivateKey) { throw "Certificate $Thumbprint has no private key in this store." }

$password = Read-Host -AsSecureString 'PFX password (min. 12 characters)'
$confirm = Read-Host -AsSecureString 'Repeat password'
$plain = [Net.NetworkCredential]::new('', $password).Password
if ($plain -ne [Net.NetworkCredential]::new('', $confirm).Password) { throw 'Passwords do not match.' }
if ($plain.Length -lt 12) { throw 'Password must be at least 12 characters.' }

if (Test-Path $Destination -PathType Container) { $Destination = Join-Path $Destination 'SapLogon-signing.pfx' }
Export-PfxCertificate -Cert $cert -FilePath $Destination -Password $password -CryptoAlgorithmOption AES256_SHA256 | Out-Null
Write-Host "Exported to $Destination"
