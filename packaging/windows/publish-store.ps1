# 商店包不自动签名、安装或上传；正式身份仅从 Partner Center 登记信息读取。
[CmdletBinding(DefaultParameterSetName = 'Store')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Store')][string]$SettingsPath,
    [Parameter(Mandatory, ParameterSetName = 'Preview')][switch]$Preview
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ($Preview) {
    # 只验证打包链路，不可上传商店，也不注册到本机。
    $settings = @{ identityName = 'Asgard.Brigitte.PackagingPreview'; publisher = 'CN=BrigittePackagingPreview'; publisherDisplayName = 'Brigitte Packaging Preview'; version = '1.0.0.0' }
} else {
    $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json -AsHashtable
}
foreach ($field in @('identityName', 'publisher', 'publisherDisplayName', 'version')) {
    if ([string]::IsNullOrWhiteSpace($settings[$field])) { throw "缺少 $field，请填写 Partner Center 登记信息。" }
}
if ($settings.identityName -notmatch '^[A-Za-z0-9.-]{3,50}$') { throw 'identityName 必须是 Partner Center 中的 Package/Identity/Name。' }
if ($settings.publisher -notmatch '^CN=') { throw 'publisher 必须完整复制 Partner Center 的 Package/Identity/Publisher。' }
if (!$Preview) {
    foreach ($field in @('privacyPolicyUrl', 'supportUrl')) {
        $link = $null
        if (![Uri]::TryCreate($settings[$field], [UriKind]::Absolute, [ref]$link) -or $link.Scheme -ne 'https' -or $link.UserInfo -ne '') {
            throw "$field 必须是已公开可访问的 HTTPS 页面。"
        }
    }
}
$version = $null
if (![Version]::TryParse($settings.version, [ref]$version) -or $version.Major -lt 1 -or $version.Revision -ne 0 -or
    $version.Major -gt 65535 -or $version.Minor -gt 65535 -or $version.Build -gt 65535) {
    throw '商店版本必须是四段数字，第一段大于 0、最后一段为 0，例如 1.0.0.0。'
}
$mode = if ($Preview) { 'store-preview' } else { 'store' }
$outputRoot = Join-Path $repoRoot "publish/$mode/$version"
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Package.appxmanifest.template') -Raw
foreach ($entry in @{ '__IDENTITY_NAME__' = $settings.identityName; '__PUBLISHER__' = $settings.publisher; '__PUBLISHER_DISPLAY_NAME__' = $settings.publisherDisplayName; '__VERSION__' = $settings.version }.GetEnumerator()) {
    $manifest = $manifest.Replace($entry.Key, [Security.SecurityElement]::Escape($entry.Value))
}
$manifestPath = Join-Path $outputRoot 'Package.appxmanifest'
[IO.File]::WriteAllText($manifestPath, $manifest, [Text.UTF8Encoding]::new($false))
$project = Join-Path $repoRoot 'src/Asgard.Brigitte.Desktop/Asgard.Brigitte.Desktop.csproj'
# 直接生成 Partner Center 接受的 MSIX；不依赖上传包装器及可选的符号压缩任务。
$publishArguments = @('publish', $project, '-c', 'Release', '-m:1', '-nr:false', '-f', 'net10.0-windows10.0.19041.0',
    '-p:RuntimeIdentifier=win-x64', '-p:WindowsPackageType=MSIX', '-p:GenerateAppxPackageOnBuild=true',
    '-p:AppxPackageSigningEnabled=false', '-p:AppxBundle=Never', '-p:UapAppxPackageBuildMode=SideloadOnly', '-p:AppxSymbolPackageEnabled=false',
    '-p:WindowsAppSDKSelfContained=true', '-p:SelfContained=true', '-p:PublishTrimmed=false',
    '-p:DefaultLanguage=en-US', "-p:BrigitteStoreManifest=$manifestPath", "-p:ApplicationId=$($settings.identityName)",
    "-p:ApplicationDisplayVersion=$($version.ToString(3))", '-p:ApplicationVersion=0',
    "-p:AppxPackageDir=$outputRoot/packages/", '-o', (Join-Path $outputRoot 'app'), '-v:minimal')
if (!$Preview) {
    # 保留 URL 中的百分号、查询参数和分隔符，避免被 MSBuild 作为多个属性或表达式处理。
    foreach ($entry in @{ 'BrigittePrivacyPolicyUrl' = $settings.privacyPolicyUrl; 'BrigitteSupportUrl' = $settings.supportUrl }.GetEnumerator()) {
        $value = $entry.Value.Replace('%', '%25').Replace(';', '%3B').Replace(',', '%2C').Replace('$', '%24').Replace('@', '%40').Replace("'", '%27').Replace('(', '%28').Replace(')', '%29').Replace('*', '%2A').Replace('?', '%3F')
        $publishArguments += "-p:$($entry.Key)=$value"
    }
}
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw "MSIX 构建失败，退出码 $LASTEXITCODE。" }
Write-Host "已生成 $mode 包：$outputRoot。尚未安装、认证或上传。"
