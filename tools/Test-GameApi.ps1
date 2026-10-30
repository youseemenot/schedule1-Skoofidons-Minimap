param(
    [Parameter(Mandatory=$true)][string]$ModPath,
    [Parameter(Mandatory=$true)][string]$GameDirectory
)

# Inspect metadata only: do not load/execute the mod or any game type.
$ErrorActionPreference = 'Stop'
$gameAssembly = Join-Path $GameDirectory 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'
$gameStream = [IO.File]::OpenRead($gameAssembly)
$modStream = [IO.File]::OpenRead($ModPath)
$gamePe = [System.Reflection.PortableExecutable.PEReader]::new($gameStream)
$modPe = [System.Reflection.PortableExecutable.PEReader]::new($modStream)
try {
    $gameReader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($gamePe)
    $modReader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($modPe)
    $methodsByType = @{}
    foreach ($handle in $gameReader.TypeDefinitions) {
        $type = $gameReader.GetTypeDefinition($handle)
        $name = $gameReader.GetString($type.Namespace) + '.' + $gameReader.GetString($type.Name)
        $methods = [Collections.Generic.HashSet[string]]::new()
        foreach ($methodHandle in $type.GetMethods()) {
            $method = $gameReader.GetMethodDefinition($methodHandle)
            [void]$methods.Add($gameReader.GetString($method.Name))
        }
        $methodsByType[$name] = $methods
    }
    $checked = 0
    $missing = [Collections.Generic.HashSet[string]]::new()
    foreach ($handle in $modReader.MemberReferences) {
        $member = $modReader.GetMemberReference($handle)
        if ($member.Parent.Kind -ne 'TypeReference') { continue }
        $typeHandle = [System.Reflection.Metadata.Ecma335.MetadataTokens]::TypeReferenceHandle(
            [System.Reflection.Metadata.Ecma335.MetadataTokens]::GetToken($member.Parent) -band 0xffffff)
        $type = $modReader.GetTypeReference($typeHandle)
        if ($type.ResolutionScope.Kind -ne 'AssemblyReference') { continue }
        $scopeHandle = [System.Reflection.Metadata.Ecma335.MetadataTokens]::AssemblyReferenceHandle(
            [System.Reflection.Metadata.Ecma335.MetadataTokens]::GetToken($type.ResolutionScope) -band 0xffffff)
        $scope = $modReader.GetAssemblyReference($scopeHandle)
        if ($modReader.GetString($scope.Name) -ne 'Assembly-CSharp') { continue }
        $typeName = $modReader.GetString($type.Namespace) + '.' + $modReader.GetString($type.Name)
        $memberName = $modReader.GetString($member.Name)
        $checked++
        if (-not $methodsByType.ContainsKey($typeName) -or -not $methodsByType[$typeName].Contains($memberName)) {
            [void]$missing.Add($typeName + '.' + $memberName)
        }
    }
    if ($missing.Count -gt 0) {
        foreach ($name in $missing) { Write-Output ('MISSING GAME API: ' + $name) }
        throw "Game API contract failed: $($missing.Count) missing member(s) in $checked direct game references."
    }
    Write-Output "Game API contract passed: $checked direct game member references exist in the installed interop assembly."
    Write-Output 'This metadata check complements compilation; it does not test generic/inherited bindings or native runtime behavior.'
} finally {
    $modPe.Dispose(); $gamePe.Dispose(); $modStream.Dispose(); $gameStream.Dispose()
}
