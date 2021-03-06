$parsers = @{ 
	provide = [regex] "goog\.provide\(([^\)]+)\)"
	require = [regex] "goog\.require\(([^\)]+)\)"
}

function Write-JsDeps {
	param($root, $prefix, $exclusion)
	
	"// This file was autogenerated"
	"// Please do not edit."
	
	Get-ChildItem $root -Recurse -Include *.js -Exclude $exclusion
		| Sort-Object FullName
		| ForEach-Object { Create-DepsLine $root $prefix $_.FullName }
}

function Create-DepsLine {
	param([string] $root, [string] $prefix, [string] $path)
	$prefixedPath = Get-PrefixedPath $root $prefix $path
	$source = Get-Content $path -ReadCount 0
	$provides = Parse 'provide' $source
	$requires = Parse 'require' $source
	"goog.addDependency('$prefixedPath', [$provides], [$requires]);"
}

function Parse {
	param([string] $what, [string] $from)
	($parsers[$what].Matches($from) | ForEach-Object { $_.Groups[1].Value }) -join ', '
}

function Get-PrefixedPath {
	param([string] $root, [string] $prefix, [string] $path)
	$path.Replace($root, $prefix).Replace('\', '/').Replace('//', '/')
}