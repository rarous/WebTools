function Deploy-Package {
    param($Project, $Configuration, $PkgLocation, $DestServer, $SiteName, $UserName, $Password)
    
    Write-Host Cleaning project files
    Exec { MsBuild $Project /t:Clean /p:Configuration=$Configuration /v:q }
    
    Write-Host "Staring building project $Project"
    Exec { MsBuild $Project /t:Build /p:Configuration=$Configuration /v:q }
    
    Write-Host "Starting packaging project $Project"
    Exec { MsBuild $Project /t:Package /p:Configuration=$Configuration /p:PackageLocation=$PkgLocation /v:m }
    
    Write-Host "Starting deployment of site $SiteName to $DestServer"
    $serverUrl ="https://${DestServer}:8172/msdeploy.axd?site=$SiteName"
    $dest = "auto,computerName=$serverUrl,username=$UserName,password=$Password,authtype=basic"
    Exec { & $MsDeploy_Dir\MsDeploy -verb:sync -source:package=$PkgLocation -dest:$dest -allowUntrusted }
}

function Transform-Config {
    param([string] $source, [string] $transform, [string] $destination = '')
    if (-not $destination) { $destination = $source; }
    Exec { MsBuild TransformHelper.build /p:Source=$source /p:Transform=$transform /p:Destination=$destination }
}
