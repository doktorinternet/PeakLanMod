param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path $_ -PathType Leaf })]
    [string] $Path
)

$patterns = @(
    'PEAK LAN (Probe|Mod)',
    'Photon state',
    'CALLBACK',
    'ConnectUsingSettings',
    'CreateRoom',
    'JoinRoom',
    'CloseConnection',
    'Disconnect',
    'LeaveRoom',
    'HostState',
    'JoinSpecificRoomState',
    'InRoomState',
    'Exception',
    '\[Error',
    '\[Warning'
)

Select-String -Path $Path -Pattern $patterns |
    ForEach-Object {
        '{0,6}: {1}' -f $_.LineNumber, $_.Line
    }
