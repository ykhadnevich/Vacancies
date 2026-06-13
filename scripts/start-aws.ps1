#requires -Version 5.1
<#
Starts previously-stopped EC2 + RDS. Re-attaches existing Elastic IP if it
got detached. Re-authorises current SSH IP. Health check at the end.

Use after stop-aws.ps1 to bring everything back online before a demo.
#>

$ErrorActionPreference = 'Stop'
$Region   = "eu-central-1"
$Ec2SgId  = "sg-039c6325884b67746"
$SshKeyPath = "$env:USERPROFILE\.ssh\vacancies-deploy-key.pem"

$statePath = "$PSScriptRoot\deploy-state.json"
if (-not (Test-Path $statePath)) { throw "No deploy-state.json found. Run deploy-restore.ps1 first." }
$state = Get-Content $statePath | ConvertFrom-Json

function Ok($msg) { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Info($msg) { Write-Host "  [..] $msg" }

# Re-authorise current IP for SSH
try {
    $myIp = (Invoke-WebRequest -Uri "https://checkip.amazonaws.com" -UseBasicParsing).Content.Trim()
    aws ec2 authorize-security-group-ingress --group-id $Ec2SgId --protocol tcp --port 22 --cidr "$myIp/32" --region $Region 2>$null | Out-Null
    Ok "SSH ingress authorised for $myIp"
} catch {}

# Start RDS
Write-Host "Starting RDS vacancies-db..." -ForegroundColor Cyan
aws rds start-db-instance --db-instance-identifier vacancies-db --region $Region --output text | Out-Null
Ok "RDS start initiated (5-10 min to ready)"

# Start EC2
Write-Host "Starting EC2 $($state.InstanceId)..." -ForegroundColor Cyan
aws ec2 start-instances --instance-ids $state.InstanceId --region $Region --output text | Out-Null
Ok "EC2 start initiated (1-2 min)"

Info "Waiting for EC2 running state..."
aws ec2 wait instance-running --instance-ids $state.InstanceId --region $Region
Ok "EC2 running"

# Check if EIP still associated; if not, re-associate
$currentEip = aws ec2 describe-instances --instance-ids $state.InstanceId --query "Reservations[0].Instances[0].PublicIpAddress" --output text --region $Region
if ($currentEip -ne $state.EipAddress) {
    Info "Re-associating Elastic IP $($state.EipAddress)..."
    aws ec2 associate-address --instance-id $state.InstanceId --allocation-id $state.EipAllocationId --region $Region | Out-Null
    Ok "EIP re-associated"
}

Info "Waiting for RDS available..."
aws rds wait db-instance-available --db-instance-identifier vacancies-db --region $Region
Ok "RDS available"

# Health check
Write-Host ""
Write-Host "Health check..." -ForegroundColor Cyan
Start-Sleep -Seconds 20

for ($i = 1; $i -le 12; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://$($state.EipAddress):8080/health" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($r.StatusCode -eq 200) {
            Ok "API healthy"
            Write-Host ""
            Write-Host "  Frontend: https://dsus1dizgh006.cloudfront.net" -ForegroundColor White
            Write-Host "  API:      https://api.vakansio.online (if DNS still points at $($state.EipAddress))" -ForegroundColor White
            exit 0
        }
    } catch {}
    Write-Host "  attempt $i/12 - waiting..."
    Start-Sleep -Seconds 10
}

Write-Host "  [!] API not responding. SSH and check:" -ForegroundColor Yellow
Write-Host "    ssh -i `"$SshKeyPath`" ec2-user@$($state.EipAddress)" -ForegroundColor White
