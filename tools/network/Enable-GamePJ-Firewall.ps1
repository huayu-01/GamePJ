#Requires -RunAsAdministrator

$rules = @(
    @{ Name = "GamePJ-Game-UDP-7000"; Port = 7000 },
    @{ Name = "GamePJ-Discovery-UDP-17341"; Port = 17341 }
)

foreach ($rule in $rules) {
    Get-NetFirewallRule -DisplayName $rule.Name -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule

    New-NetFirewallRule `
        -DisplayName $rule.Name `
        -Direction Inbound `
        -Action Allow `
        -Protocol UDP `
        -LocalPort $rule.Port `
        -Profile Private | Out-Null
}

Write-Host "GamePJ UDP 7000/17341 已允许通过 Windows 专用网络防火墙。"
