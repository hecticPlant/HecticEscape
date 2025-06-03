# PowerShell-Skript: SetAutostart.ps1
param (
    [Parameter(Mandatory=$true)]
    [bool]$Enable,
    [Parameter(Mandatory=$true)]
    [string]$ApplicationPath
)

try {
    $RegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    $AppName = "HecticEscape"
    
    if ($Enable) {
        # Füge Autostart-Eintrag hinzu mit /minimized Parameter
        Set-ItemProperty -Path $RegistryPath -Name $AppName -Value "`"$ApplicationPath`" /minimized" -ErrorAction Stop
        Write-Output "Autostart-Eintrag für HecticEscape wurde gesetzt: $ApplicationPath"
    }
    else {
        # Entferne Autostart-Eintrag falls vorhanden
        if (Get-ItemProperty -Path $RegistryPath -Name $AppName -ErrorAction SilentlyContinue) {
            Remove-ItemProperty -Path $RegistryPath -Name $AppName -ErrorAction Stop
            Write-Output "Autostart-Eintrag für HecticEscape wurde entfernt"
        }
    }
    exit 0
}
catch {
    Write-Error "Fehler beim Setzen des Autostart-Eintrags: $_"
    exit 1
}

