#requires -Version 5.1
<#
Restores Vakansio production infra from existing snapshots.
Idempotent where possible. Run with -DryRun first to preview.

Usage:
  .\scripts\deploy-restore.ps1 -DryRun    # show what will happen
  .\scripts\deploy-restore.ps1            # actually do it
#>
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# ===== CONFIG =====
$Region              = "eu-central-1"
$AmiId               = "ami-012d676c63c268a30"
$DbSnapshotId        = "vacancies-db-defense-snapshot-001"
$DbInstanceId        = "vacancies-db"
$Ec2SgId             = "sg-039c6325884b67746"
$RdsSgId             = "sg-0315d1fde0dda5e07"
$SubnetGroupName     = "vacancies-db-subnet-group"
$InstanceProfileName = "vacancies-ec2-profile"
$VpcId               = "vpc-028588d2ef59c34f8"
$KeyName             = "vacancies-deploy-key"
$SshKeyPath          = "$env:USERPROFILE\.ssh\vacancies-deploy-key.pem"
$CloudFrontDomain    = "dsus1dizgh006.cloudfront.net"

function Step($n, $title) {
    Write-Host ""
    Write-Host "===== Step $n - $title =====" -ForegroundColor Cyan
}
function Ok($msg)   { Write-Host "  [OK]  $msg" -ForegroundColor Green }
function Info($msg) { Write-Host "  [..]  $msg" }
function Warn($msg) { Write-Host "  [!]   $msg" -ForegroundColor Yellow }

# ===== Step 0 - preflight =====
Step 0 "Preflight"
$account = aws sts get-caller-identity --query "Account" --output text 2>$null
if (-not $account) { throw "AWS CLI auth failed" }
Ok "AWS account: $account"

if (-not (Test-Path $SshKeyPath)) { throw "SSH key missing: $SshKeyPath" }
Ok "SSH key found"

$amiState = aws ec2 describe-images --image-ids $AmiId --query "Images[].State" --output text --region $Region
if ($amiState -ne "available") { throw "AMI not available: $amiState" }
Ok "AMI available"

$snapState = aws rds describe-db-snapshots --db-snapshot-identifier $DbSnapshotId --query "DBSnapshots[].Status" --output text --region $Region
if ($snapState -ne "available") { throw "Snapshot not available: $snapState" }
Ok "RDS snapshot available"

if ($DryRun) {
    Write-Host ""
    Write-Host "[DRY RUN] All checks passed. Run without -DryRun to deploy." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Continue? Costs ~`$2-3 for 2 weeks active. (y/N): " -ForegroundColor Yellow -NoNewline
$confirm = Read-Host
if ($confirm -ne "y") { exit 0 }

# ===== Step 1 - authorize current IP for SSH =====
Step 1 "Authorize current IP for SSH"
try {
    $myIp = (Invoke-WebRequest -Uri "https://checkip.amazonaws.com" -UseBasicParsing).Content.Trim()
    Info "Your IP: $myIp"
    aws ec2 authorize-security-group-ingress --group-id $Ec2SgId --protocol tcp --port 22 --cidr "$myIp/32" --region $Region 2>$null | Out-Null
    Ok "SSH ingress added (or already present)"
} catch {
    Warn "Could not auto-authorize IP - you may need to add manually if SSH fails"
}

# ===== Step 2 - start RDS restore (async) =====
Step 2 "Restoring RDS from snapshot (async, runs 5-10 min in background)"
$existingRds = $null
try {
    $existingRds = aws rds describe-db-instances --db-instance-identifier $DbInstanceId --query "DBInstances[].DBInstanceStatus" --output text --region $Region 2>$null
} catch { $existingRds = $null }
if ($existingRds) {
    Warn "RDS '$DbInstanceId' already exists ($existingRds) - skipping restore"
} else {
    aws rds restore-db-instance-from-db-snapshot `
        --db-instance-identifier $DbInstanceId `
        --db-snapshot-identifier $DbSnapshotId `
        --db-instance-class db.t3.micro `
        --no-multi-az `
        --no-publicly-accessible `
        --db-subnet-group-name $SubnetGroupName `
        --vpc-security-group-ids $RdsSgId `
        --no-auto-minor-version-upgrade `
        --region $Region `
        --output json | Out-Null
    Ok "RDS restore initiated"
}

# ===== Step 3 - launch EC2 from AMI =====
Step 3 "Launching EC2 from AMI"
$subnetId = aws ec2 describe-subnets --filters "Name=vpc-id,Values=$VpcId" "Name=default-for-az,Values=true" --query "Subnets[0].SubnetId" --output text --region $Region
Info "Subnet: $subnetId"

$instanceId = aws ec2 run-instances `
    --image-id $AmiId `
    --instance-type t3.micro `
    --key-name $KeyName `
    --security-group-ids $Ec2SgId `
    --subnet-id $subnetId `
    --iam-instance-profile "Name=$InstanceProfileName" `
    --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=vacancies-api},{Key=Purpose,Value=thesis-defense}]" `
    --query "Instances[0].InstanceId" `
    --output text `
    --region $Region

Ok "Instance launched: $instanceId"
Info "Waiting for running state (1-2 min)..."
aws ec2 wait instance-running --instance-ids $instanceId --region $Region
Ok "Instance running"

# ===== Step 4 - allocate + associate Elastic IP =====
Step 4 "Allocating new Elastic IP"
$eip = aws ec2 allocate-address --domain vpc --region $Region | ConvertFrom-Json
$eipAddress      = $eip.PublicIp
$eipAllocationId = $eip.AllocationId
Ok "Elastic IP: $eipAddress (allocation: $eipAllocationId)"

aws ec2 associate-address --instance-id $instanceId --allocation-id $eipAllocationId --region $Region | Out-Null
Ok "Associated to $instanceId"

# ===== Step 5 - wait for RDS =====
Step 5 "Waiting for RDS to become available (5-10 min)"
aws rds wait db-instance-available --db-instance-identifier $DbInstanceId --region $Region
$newRdsEndpoint = aws rds describe-db-instances --db-instance-identifier $DbInstanceId --query "DBInstances[0].Endpoint.Address" --output text --region $Region
Ok "RDS endpoint: $newRdsEndpoint"

# ===== Step 6 - update SSM ConnectionString =====
Step 6 "Updating SSM ConnectionString with new RDS endpoint"
$oldConnStr = aws ssm get-parameter --name "/vacancies/prod/ConnectionStrings/DefaultConnection" --with-decryption --query "Parameter.Value" --output text --region $Region
$newConnStr = $oldConnStr -replace "Host=[^;]+", "Host=$newRdsEndpoint"
aws ssm put-parameter --name "/vacancies/prod/ConnectionStrings/DefaultConnection" --value "$newConnStr" --type SecureString --overwrite --region $Region | Out-Null
Ok "ConnectionString updated"

# ===== Step 7 - SSH bootstrap =====
Step 7 "Bootstrapping API container on EC2"
Info "Waiting 30s for SSH daemon..."
Start-Sleep -Seconds 30

try { ssh-keygen -R $eipAddress 2>$null | Out-Null } catch {}
try { ssh-keyscan -H $eipAddress 2>$null | Out-File -Append "$env:USERPROFILE\.ssh\known_hosts" -Encoding ASCII } catch {}

$bootstrap = @"
cd /home/ec2-user/vacancies && \
docker compose -f docker-compose.production.yml down 2>/dev/null; \
docker compose -f docker-compose.production.yml up -d && \
sleep 5 && \
docker compose -f docker-compose.production.yml ps
"@

Info "Running bootstrap (may take a minute)..."
ssh -i $SshKeyPath -o StrictHostKeyChecking=no "ec2-user@$eipAddress" $bootstrap

# ===== Step 8 - health check =====
Step 8 "Health check"
Start-Sleep -Seconds 15

$healthy = $false
for ($i = 1; $i -le 12; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://${eipAddress}:8080/health" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($r.StatusCode -eq 200) { $healthy = $true; break }
    } catch {}
    Write-Host "  attempt $i/12 - waiting..."
    Start-Sleep -Seconds 10
}

if ($healthy) {
    Ok "API healthy on port 8080"
} else {
    Warn "API not responding yet - debug with: ssh -i `"$SshKeyPath`" ec2-user@$eipAddress"
}

# ===== Step 9 - summary =====
Step 9 "Done"
Write-Host ""
Write-Host "  EC2 instance:    $instanceId"               -ForegroundColor White
Write-Host "  Elastic IP:      $eipAddress"               -ForegroundColor White
Write-Host "  EIP allocation:  $eipAllocationId"          -ForegroundColor White
Write-Host "  RDS endpoint:    $newRdsEndpoint"           -ForegroundColor White
Write-Host "  Frontend:        https://$CloudFrontDomain" -ForegroundColor White
Write-Host "  API direct:      http://${eipAddress}:8080/health" -ForegroundColor White
Write-Host "  SSH:             ssh -i `"$SshKeyPath`" ec2-user@$eipAddress" -ForegroundColor White
Write-Host ""
Write-Host "  [!] UPDATE DNS at Namecheap:" -ForegroundColor Yellow
Write-Host "    Login -> vakansio.online -> Manage -> Advanced DNS"
Write-Host "    Edit A record: Host=api  ->  Value=$eipAddress  (TTL: 1 min)"
Write-Host "    After ~5 min: https://api.vakansio.online/health should respond"
Write-Host ""
Write-Host "  Next: push code to main to trigger GitHub Actions build + deploy" -ForegroundColor Yellow
Write-Host ""

# Save state for stop script
$state = @{
    InstanceId      = $instanceId
    EipAllocationId = $eipAllocationId
    EipAddress      = $eipAddress
    RdsEndpoint     = $newRdsEndpoint
    DeployedAt      = (Get-Date).ToString("o")
} | ConvertTo-Json
$state | Out-File "$PSScriptRoot\deploy-state.json" -Encoding UTF8
Ok "State saved to scripts/deploy-state.json"
