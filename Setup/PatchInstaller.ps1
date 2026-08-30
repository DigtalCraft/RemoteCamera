<#
.SYNOPSIS
RemoteCameraのMSIへ自動起動設定を追加する。

.DESCRIPTION
スタートアップショートカットをMSI管理に変更し、
旧版で残る可能性があるRunレジストリ値をインストール時に削除する。
#>

#requires -Version 7.0

$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
COMオブジェクトのメソッドを呼び出す。

.PARAMETER ComObject
呼び出し対象のCOMオブジェクト。

.PARAMETER MethodName
メソッド名。

.PARAMETER Arguments
メソッドへ渡す引数。
#>
function Invoke-ComMethod {
    param(
        [Parameter(Mandatory)]
        [object]$ComObject,

        [Parameter(Mandatory)]
        [string]$MethodName,

        [object[]]$Arguments = @()
    )

    return $ComObject.GetType().InvokeMember(
        $MethodName,
        [System.Reflection.BindingFlags]::InvokeMethod,
        $null,
        $ComObject,
        $Arguments
    )
}

<#
.SYNOPSIS
COMオブジェクトのプロパティを取得する。

.PARAMETER ComObject
取得対象のCOMオブジェクト。

.PARAMETER PropertyName
プロパティ名。

.PARAMETER Arguments
インデックスプロパティへ渡す引数。
#>
function Get-ComProperty {
    param(
        [Parameter(Mandatory)]
        [object]$ComObject,

        [Parameter(Mandatory)]
        [string]$PropertyName,

        [object[]]$Arguments = @()
    )

    return $ComObject.GetType().InvokeMember(
        $PropertyName,
        [System.Reflection.BindingFlags]::GetProperty,
        $null,
        $ComObject,
        $Arguments
    )
}

<#
.SYNOPSIS
MSIのSQLを実行する。

.PARAMETER Database
MSIデータベース。

.PARAMETER Sql
実行するSQL。
#>
function Invoke-MsiNonQuery {
    param(
        [Parameter(Mandatory)]
        [object]$Database,

        [Parameter(Mandatory)]
        [string]$Sql
    )

    $view = Invoke-ComMethod -ComObject $Database -MethodName 'OpenView' -Arguments @($Sql)
    try {
        [void](Invoke-ComMethod -ComObject $view -MethodName 'Execute')
    }
    finally {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    }
}

<#
.SYNOPSIS
指定したSQLに該当するレコードが存在するか確認する。

.PARAMETER Database
MSIデータベース。

.PARAMETER Sql
検索SQL。

.OUTPUTS
レコードが存在する場合はtrue。
#>
function Test-MsiRecord {
    param(
        [Parameter(Mandatory)]
        [object]$Database,

        [Parameter(Mandatory)]
        [string]$Sql
    )

    $view = Invoke-ComMethod -ComObject $Database -MethodName 'OpenView' -Arguments @($Sql)
    try {
        [void](Invoke-ComMethod -ComObject $view -MethodName 'Execute')
        $record = Invoke-ComMethod -ComObject $view -MethodName 'Fetch'
        if ($null -eq $record) {
            return $false
        }

        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
        return $true
    }
    finally {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    }
}

<#
.SYNOPSIS
RemoteCamera.exeを管理するコンポーネントIDを取得する。

.PARAMETER Database
MSIデータベース。

.OUTPUTS
RemoteCamera.exeのコンポーネントID。
#>
function Get-RemoteCameraComponent {
    param(
        [Parameter(Mandatory)]
        [object]$Database
    )

    $view = Invoke-ComMethod -ComObject $Database -MethodName 'OpenView' -Arguments @('SELECT `Component_`,`FileName` FROM `File`')
    try {
        [void](Invoke-ComMethod -ComObject $view -MethodName 'Execute')

        while ($record = Invoke-ComMethod -ComObject $view -MethodName 'Fetch') {
            try {
                $fileName = Get-ComProperty -ComObject $record -PropertyName 'StringData' -Arguments @(2)
                $longFileName = ($fileName -split '\|')[-1]
                if ($longFileName -eq 'RemoteCamera.exe') {
                    return Get-ComProperty -ComObject $record -PropertyName 'StringData' -Arguments @(1)
                }
            }
            finally {
                [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
            }
        }
    }
    finally {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    }

    throw 'MSI内にRemoteCamera.exeが見つかりません。'
}

<#
.SYNOPSIS
RemoteCameraのMSIへ自動起動設定を反映する。

.PARAMETER MsiPath
更新するMSIのパス。
#>
function Update-RemoteCameraInstaller {
    param(
        [Parameter(Mandatory)]
        [string]$MsiPath
    )

    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $null

    try {
        $database = Invoke-ComMethod -ComObject $installer -MethodName 'OpenDatabase' -Arguments @($MsiPath, 1)
        $componentId = Get-RemoteCameraComponent -Database $database

        if (-not (Test-MsiRecord -Database $database -Sql 'SELECT `Directory` FROM `Directory` WHERE `Directory`=''StartupFolder''')) {
            Invoke-MsiNonQuery -Database $database -Sql 'INSERT INTO `Directory` (`Directory`,`Directory_Parent`,`DefaultDir`) VALUES (''StartupFolder'',''TARGETDIR'',''.'')'
        }

        Invoke-MsiNonQuery -Database $database -Sql 'DELETE FROM `Shortcut` WHERE `Shortcut`=''RemoteCameraStartupShortcut'''
        $shortcutSql = "INSERT INTO ``Shortcut`` (``Shortcut``,``Directory_``,``Name``,``Component_``,``Target``,``Description``,``ShowCmd``,``WkDir``) VALUES ('RemoteCameraStartupShortcut','StartupFolder','REMOTE~1|RemoteCamera','$componentId','DefaultFeature','RemoteCamera',1,'TARGETDIR')"
        Invoke-MsiNonQuery -Database $database -Sql $shortcutSql

        Invoke-MsiNonQuery -Database $database -Sql 'DELETE FROM `RemoveRegistry` WHERE `RemoveRegistry`=''RemoveLegacyRemoteCameraRun'''
        $registrySql = "INSERT INTO ``RemoveRegistry`` (``RemoveRegistry``,``Root``,``Key``,``Name``,``Component_``) VALUES ('RemoveLegacyRemoteCameraRun',1,'Software\Microsoft\Windows\CurrentVersion\Run','RemoteCamera','$componentId')"
        Invoke-MsiNonQuery -Database $database -Sql $registrySql

        [void](Invoke-ComMethod -ComObject $database -MethodName 'Commit')
        Write-Host "MSIの自動起動設定を更新しました: $MsiPath"
    }
    finally {
        if ($null -ne $database) {
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
        }

        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
    }
}

$msiPaths = @(
    (Join-Path $PSScriptRoot 'Debug\Setup.msi'),
    (Join-Path $PSScriptRoot 'Release\Setup.msi')
)

foreach ($msiPath in $msiPaths) {
    if (Test-Path -LiteralPath $msiPath) {
        Update-RemoteCameraInstaller -MsiPath (Resolve-Path -LiteralPath $msiPath).Path
    }
}
