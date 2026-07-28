$managed = Join-Path $PWD "PEAK_Data\Managed"

Get-ChildItem $managed -Filter "*.dll" |
    Where-Object {
        $_.Name -match "Photon|Assembly-CSharp|Steam|Lobby|Voice"
    } |
    ForEach-Object {
        try {
            $assembly = [Reflection.AssemblyName]::GetAssemblyName($_.FullName)

            [PSCustomObject]@{
                File    = $_.Name
                Assembly = $assembly.Name
                Version = $assembly.Version
                SHA256  = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
            }
        }
        catch {
            [PSCustomObject]@{
                File    = $_.Name
                Assembly = "<not managed>"
                Version = ""
                SHA256  = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
            }
        }
    } |
    Export-Csv "E:\Code\PEAK_LAN_MOD\notes\assemblies_$(Get-Date -Format 'yyyy_MM_dd_HHmmss').csv" -NoTypeInformation