# PowerShell-Skript: SetAutostart.ps1

param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("True","False")]
    [string]$Enable,

    [Parameter(Mandatory = $true)]
    [string]$ApplicationPath
)

if    ($Enable -eq "True")  { $EnableBool = $true  }
elseif ($Enable -eq "False") { $EnableBool = $false }
else  { throw "Ungültiger Enable-Wert: '$Enable'. Erwartet ist 'True' oder 'False'." }


Write-Output "Script start: Enable=$EnableBool, ApplicationPath=$ApplicationPath"

try {
    $RegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    $AppName      = "HecticEscape"
    
    if ($EnableBool) {
        Write-Verbose "Füge Autostart-Eintrag hinzu..."
        Set-ItemProperty -Path $RegistryPath `
                         -Name $AppName `
                         -Value "`"$ApplicationPath`"" `
                         -ErrorAction Stop

        Write-Output "Autostart-Eintrag für HecticEscape wurde gesetzt: $ApplicationPath"
    }
    else {
        Write-Verbose "Entferne Autostart-Eintrag (falls vorhanden)..."
        if (Get-ItemProperty -Path $RegistryPath `
                             -Name $AppName `
                             -ErrorAction SilentlyContinue)
        {
            Remove-ItemProperty -Path $RegistryPath `
                                -Name $AppName `
                                -ErrorAction Stop

            Write-Output "Autostart-Eintrag für HecticEscape wurde entfernt"
        }
        else {
            Write-Output "Kein Autostart-Eintrag gefunden, nichts zu tun."
        }
    }

    exit 0
}
catch {
    Write-Error "Fehler beim Setzen des Autostart-Eintrags: $_"
    exit 1
}
