param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Security

$credentialDirectory = Join-Path $env:LOCALAPPDATA 'GamePJ\ArtPipeline'
$credentialPath = Join-Path $credentialDirectory 'hunyuan-api-key.json'
$dialog = New-Object System.Windows.Forms.Form
$dialog.Text = '混元生 3D API Key'
$dialog.ClientSize = New-Object System.Drawing.Size(540, 220)
$dialog.StartPosition = 'CenterScreen'
$dialog.FormBorderStyle = 'FixedDialog'
$dialog.MaximizeBox = $false
$dialog.MinimizeBox = $false
$dialog.TopMost = $true

$description = New-Object System.Windows.Forms.Label
$description.Text = "请粘贴腾讯混元生 3D 的 API Key。`n接口：api.ai3d.cloud.tencent.com`n密钥会加密保存在当前 Windows 用户目录，不写入项目。"
$description.SetBounds(18, 18, 505, 65)
$dialog.Controls.Add($description)

$keyInput = New-Object System.Windows.Forms.TextBox
$keyInput.UseSystemPasswordChar = $true
$keyInput.SetBounds(18, 92, 500, 26)
$dialog.Controls.Add($keyInput)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.SetBounds(18, 132, 500, 26)
$dialog.Controls.Add($statusLabel)

$saveButton = New-Object System.Windows.Forms.Button
$saveButton.Text = '加密保存'
$saveButton.SetBounds(308, 172, 100, 30)
$saveButton.Add_Click({
    try {
        $apiKeyValue = $keyInput.Text.Trim()
        if ($apiKeyValue.StartsWith('Bearer ')) { $apiKeyValue = $apiKeyValue.Substring(7).Trim() }
        if ($apiKeyValue.Length -lt 8 -or $apiKeyValue -match '\s') {
            $statusLabel.Text = '请输入完整的 API Key，不要包含 curl 命令或空格。'
            return
        }
        # DPAPI 将密钥绑定到当前 Windows 用户，项目中不保存明文。
        $plainBytes = [System.Text.Encoding]::UTF8.GetBytes($apiKeyValue)
        $encryptedBytes = [System.Security.Cryptography.ProtectedData]::Protect(
            $plainBytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
        $record = @{
            version = 1
            api_origin = 'https://api.ai3d.cloud.tencent.com'
            protected_key = [Convert]::ToBase64String($encryptedBytes)
        }
        New-Item -ItemType Directory -Path $credentialDirectory -Force | Out-Null
        [System.IO.File]::WriteAllText($credentialPath, ($record | ConvertTo-Json), [System.Text.UTF8Encoding]::new($false))
        [Array]::Clear($plainBytes, 0, $plainBytes.Length)
        $apiKeyValue = $null
        $keyInput.Clear()
        [System.Windows.Forms.MessageBox]::Show('密钥已加密保存。返回对话告诉我“已保存”，即可检查接入。', '已保存') | Out-Null
        $dialog.Close()
    }
    catch {
        $statusLabel.Text = '保存失败，请确认当前用户有本地应用数据目录的写入权限。'
    }
})
$dialog.Controls.Add($saveButton)
$dialog.AcceptButton = $saveButton

$cancelButton = New-Object System.Windows.Forms.Button
$cancelButton.Text = '取消'
$cancelButton.SetBounds(418, 172, 100, 30)
$cancelButton.Add_Click({ $dialog.Close() })
$dialog.Controls.Add($cancelButton)
$dialog.CancelButton = $cancelButton
$dialog.Add_Shown({ $keyInput.Focus() })
[void]$dialog.ShowDialog()
$dialog.Dispose()
