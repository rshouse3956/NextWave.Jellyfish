<#
Common functions to leverage websocket requests to an internal server.

#>

<#
Send a websocket command to the server and optionally format the results back as json
Usage:
SendCommand -uri -cmd [-convertToJson]

Returns:
    Custom Object
    {
        Succeeded = [$true|$false]
        Data = Results of the call, optionally formatted as json; $null if failed.
        Exception = $null if succeeded; exception if failed.
    }
. ./WebSockets.ps1

$results = SendCommand "ws://controller-host:9000/ws" '{"cmd": "toCtlrGet", "get": [["patternFileList"]]}'
#>
function SendCommand{
    [CmdletBinding(SupportsShouldProcess=$false)]
    param(
        [Parameter(Mandatory,ValueFromPipelineByPropertyName)]
        [string]$uri,
        [Parameter(Mandatory,ValueFromPipelineByPropertyName)]
        [string]$cmd,
        [Parameter(ValueFromPipelineByPropertyName)]
        [bool]$convertToJson = $false
    )
    begin{
        # Create a new ClientWebSocket instance
        $webSocket = [System.Net.WebSockets.ClientWebSocket]::new()
    }
    process{
        try {
            $message = $cmd
            #Connect to the server
            $uri = [System.Uri]::new($uri)
            $webSocket.ConnectAsync($uri, [System.Threading.CancellationToken]::None).Wait() > $null
            $buffer = [System.Text.Encoding]::UTF8.GetBytes($message)
            $segment = [System.ArraySegment[Byte]]::new($buffer)
            $webSocket.SendAsync($segment,[System.Net.WebSockets.WebSocketMessageType]::Text, $true,[System.Threading.CancellationToken]::None).Wait() > $null
            #Receive message from server
            $receiveBuffer = [byte[]]::new(32384)
            $receiveSegment = [System.ArraySegment[byte]]::new($receiveBuffer)
            $result = $webSocket.ReceiveAsync($receiveSegment,[System.Threading.CancellationToken]::None).Result
            $data = [System.Text.encoding]::UTF8.GetString($receiveBuffer, 0, $result.Count)
            #Convert to json
            if($convertToJson -eq $true){
                $data = $data | Convertfrom-Json
            }
            return [PSCustomObject]@{
                Succeeded = $true
                Data = $data
                Exception = $null
            }       
        }
        catch {
            return [PSCustomObject]@{
                Succeeded = $false
                Data = $null
                Exception = $_
            }
        }
    }
    end{
        #Close the connection only if it was opened successfully.
        if($null -ne $webSocket -and $webSocket.State -eq [System.Net.WebSockets.WebSocketState]::Open){
            try {
                Write-Verbose "Closing websocket connection to $uri, state: $($webSocket.State)"
                $webSocket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Closing",[System.Threading.CancellationToken]::None).Wait() > $null
            }
            catch {
                Write-Verbose "WebSocket close skipped: $($_.Exception.Message)"
            }
        }
    }
}