

function Inline-Resources {
    param(
        [switch] $skip
    )
}

function Convert-ToBase64 {
    param([string] $path)
    [convert]::ToBase64String((Get-Content $path -encoding byte))
}