
function Optimize-Gif {
    param([string] $path)
    
    $tmp = Resolve-Path .\
    $tmp = Join-Path $tmp "tmp"
    
    if ((Test-Path -path $tmp) -ne $true) { mkdir "tmp" }
    
    $input = Get-Item $path
    $out = Join-Path $tmp "$($input.basename).png"
    
    & convert $path png:"$out"
    Optimize-Png $out -keep
    
    $orig_size = $input.length
    $new_size = (Get-Item $out).length
    
    if ($orig_size -le $new_size) {
        Write-Host "Optimized file is bigger than original"
    }
    else {
        Write-Host "Converted to PNG"
        Write-Host "Saved size: $($orig_size - $new_size) B"
        Copy-Item -path $out -destination $input.directory
    }
    
    Remove-Item $tmp -force -recurse
}

function Optimize-Png {
    param([string] $path, [switch] $keep)
    
    $tmp = Resolve-Path .\
    $tmp = Join-Path $tmp "tmp"
    
    if (!(Test-Path -path $tmp)) { mkdir "tmp" }
    
    $input = Get-Item $path
    $out = Join-Path $tmp "tmp_$($input.name)"
    
    tools\pngcrush -q -rem alla -brute -reduce $path $out
    
    $orig_size = $input.length
    $new_size = (Get-Item $out).length
    
    if ($orig_size -le $new_size) {
        Write-Host "Optimized file is bigger than original"
        Remove-Item $out
    }
    else {
        Write-Host "Saved size: $($orig_size - $new_size) B ($($new_size / $orig_size * 100) %)"
        Copy-Item -path $out -destination $input
    }
    
    if (!$keep) { Remove-Item $tmp -force -recurse }
}

function Optimize-Jpeg {
    param([string] $path)
    
    tools\jpegtran -copy none -optimize -perfect $path
}