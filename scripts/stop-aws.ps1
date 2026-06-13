#requires -Version 5.1
<#
Stops EC2 + RDS between demo sessions to minimise spend. Keeps Elastic IP
attached (~$0.10/day cost) so DNS doesn't break. Keeps S3/CloudFront/SSM
intact (zero cost when idle).

Total cost while stopped: ~$0.10/day for EIP + ~$0.50/month for Route 53
(if used) + RDS storage (~$0.10/GB/month).

To restart: .\scripts\start-aws.ps1
#>

$ErrorActionPreference = 'Stop'
$Region = "eu-central-1"

$statePath = "$PSScriptRoot\deploy-state.json"
if (-not (Test-Path $statePath)) {
    throw "No deploy-state.json found. Run deploy-restore.ps1 first."
}
$state = Get-Content $statePath | ConvertFrom-Json

Write-Host "Stopping EC2 $($state.InstanceId)..." -ForegroundColor Cyan
aws ec2 stop-instances --instance-ids $state.InstanceId --region $Region --output text | Out-Null
Write-Host "  [OK] stop initiated" -ForegroundColor Green

Write-Host "Stopping RDS vacancies-db..." -ForegroundColor Cyan
aws rds stop-db-instance --db-instance-identifier vacancies-db --region $Region --output text | Out-Null
Write-Host "  [OK] stop initiated" -ForegroundColor Green

Write-Host ""
Write-Host "  [!] RDS auto-restarts after 7 days max. Restart manually before then or" -ForegroundColor Yellow
Write-Host "      AWS will start it for you and charge full price." -ForegroundColor Yellow
Write-Host ""
Write-Host "  To restart everything: .\scripts\start-aws.ps1" -ForegroundColor White
