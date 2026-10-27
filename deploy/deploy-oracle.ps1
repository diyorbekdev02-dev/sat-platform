# One-shot deployment of the SAT Platform to Oracle Cloud Always Free:
#   VCN + subnet + firewall  ->  VM (ARM A1.Flex 4 OCPU/24 GB, fallback E2.1.Micro)  ->  Docker: PostgreSQL + app + Caddy (HTTPS)
#
# Needs:
#   ~/.oci/config + ~/.oci/oci_api_key.pem  - cloud.oracle.com -> My profile -> API keys (key_file=/oracle/.oci/oci_api_key.pem)
#   Docker Desktop                          - the OCI CLI runs from the ghcr.io/oracle/oci-cli image; nothing else is installed
#   $env:TELEGRAM_BOT_TOKEN                 - @sat_notificationbot (taken from the Render service when RENDER_API_KEY is set)
# Optional: $env:RENDER_API_KEY (copies bot token / admin phone / old DATABASE_URL from Render), $env:SAT_MIGRATE_FROM_URL
#           (postgres:// of the old database, imported once into the empty VM database), $env:SAT_ADMIN_PHONE, $env:SAT_ADMIN_PASSWORD,
#           $env:SAT_DEMO_PASSWORD, $env:SAT_SEED_DEMO (true/false), $env:SAT_CENTER_NAME, $env:OCI_COMPARTMENT_ID (default: root)
# Keys may live in deploy\secrets.local.ps1 (git-ignored): one `$env:NAME = '...'` line per key.
#
# Usage:  powershell -File deploy\deploy-oracle.ps1 [-Shape auto|A1|Micro] [-Domain sat.example.com] [-CapacityRetryMinutes 20]
#         Re-runnable: existing VCN / VM / database are reused; only the source is re-uploaded and the containers rebuilt.
param(
    [ValidateSet('auto', 'A1', 'Micro')] [string]$Shape = 'auto',
    [string]$Domain,
    [int]$CapacityRetryMinutes = 20
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$secrets = Join-Path $PSScriptRoot 'secrets.local.ps1'
if (Test-Path $secrets) { . $secrets }

$Root = Split-Path -Parent $PSScriptRoot
$Name = 'sat-platform'
$OciDir = if ($env:OCI_CLI_CONFIG_DIR) { $env:OCI_CLI_CONFIG_DIR } else { Join-Path $HOME '.oci' }
$Work = Join-Path $env:LOCALAPPDATA 'sat-oracle-deploy'   # long path: Docker cannot mount 8.3 short names
$KeyPath = Join-Path $PSScriptRoot 'oracle_ssh_key'
$KnownHosts = (Join-Path $PSScriptRoot 'oracle_known_hosts') -replace '\\', '/'
$StateFile = Join-Path $PSScriptRoot 'oracle.env'
New-Item -ItemType Directory -Force $Work | Out-Null

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }
function Write-Lf($path, $text) { [IO.File]::WriteAllText($path, ($text -replace "`r`n", "`n"), (New-Object Text.UTF8Encoding $false)) }
function Copy-Lf($src, $dst) { Write-Lf $dst ([IO.File]::ReadAllText($src)) }
function Random-Secret($n = 32) { -join ((48..57) + (97..122) | Get-Random -Count $n | ForEach-Object { [char]$_ }) }

# ---------------------------------------------------------------- OCI CLI (Docker)
$configPath = Join-Path $OciDir 'config'
if (-not (Test-Path $configPath)) { throw "OCI config not found: $configPath (cloud.oracle.com -> My profile -> API keys)" }
$Tenancy = ((Get-Content $configPath) | Where-Object { $_ -match '^\s*tenancy\s*=' } | Select-Object -First 1) -replace '^\s*tenancy\s*=\s*', ''
if (-not $Tenancy) { throw "tenancy= missing in $configPath" }
$Compartment = if ($env:OCI_COMPARTMENT_ID) { $env:OCI_COMPARTMENT_ID } else { $Tenancy }
$script:OciError = ''

function Oci {
    # Runs the OCI CLI inside Docker; returns parsed JSON (.data) or $null. Stderr text is kept in $script:OciError.
    $ErrorActionPreference = 'Continue'
    $all = & docker run --rm -e OCI_CLI_SUPPRESS_FILE_PERMISSIONS_WARNING=True -v "${OciDir}:/oracle/.oci" -v "${Work}:/work" ghcr.io/oracle/oci-cli:latest @args 2>&1
    $code = $LASTEXITCODE
    $out = ($all | Where-Object { $_ -is [string] }) -join "`n"
    $script:OciError = ($all | Where-Object { $_ -isnot [string] } | ForEach-Object { $_.ToString() }) -join "`n"
    if ($code -ne 0) { return $null }
    if ($out.Trim()) { try { return (ConvertFrom-Json $out).data } catch { return $out } }
    return $null
}
function OciOrThrow { $r = Oci @args; if ($null -eq $r -and $script:OciError -match 'Error|error') { throw "oci $($args[0..2] -join ' ') failed:`n$($script:OciError)" }; return $r }

Step "OCI: account"
$region = ((Get-Content $configPath) | Where-Object { $_ -match '^\s*region\s*=' } | Select-Object -First 1) -replace '^\s*region\s*=\s*', ''
$ads = OciOrThrow iam availability-domain list --compartment-id $Compartment
if (-not $ads) { throw "cannot list availability domains (check ~/.oci/config and the key file)" }
Write-Host "region: $region, availability domains: $(@($ads).Count)"

# ---------------------------------------------------------------- Network
Step "OCI: network"
$vcn = (Oci network vcn list --compartment-id $Compartment --display-name 'sat-vcn' --lifecycle-state AVAILABLE) | Select-Object -First 1
if (-not $vcn) {
    $vcn = OciOrThrow network vcn create --compartment-id $Compartment --cidr-block '10.0.0.0/16' --display-name 'sat-vcn' --dns-label 'satvcn' --wait-for-state AVAILABLE
    Write-Host "vcn created"
} else { Write-Host "vcn exists" }
$vcnId = $vcn.id

$igw = (Oci network internet-gateway list --compartment-id $Compartment --vcn-id $vcnId --lifecycle-state AVAILABLE) | Select-Object -First 1
if (-not $igw) {
    $igw = OciOrThrow network internet-gateway create --compartment-id $Compartment --vcn-id $vcnId --is-enabled true --display-name 'sat-igw' --wait-for-state AVAILABLE
    Write-Host "internet gateway created"
}

Write-Lf (Join-Path $Work 'routes.json') (ConvertTo-Json @(@{ destination = '0.0.0.0/0'; destinationType = 'CIDR_BLOCK'; networkEntityId = $igw.id }))
OciOrThrow network route-table update --rt-id $vcn.'default-route-table-id' --route-rules 'file:///work/routes.json' --force | Out-Null

$ingress = @(22, 80, 443) | ForEach-Object { @{ protocol = '6'; source = '0.0.0.0/0'; sourceType = 'CIDR_BLOCK'; isStateless = $false; tcpOptions = @{ destinationPortRange = @{ min = $_; max = $_ } } } }
$ingress += @{ protocol = '1'; source = '0.0.0.0/0'; sourceType = 'CIDR_BLOCK'; isStateless = $false; icmpOptions = @{ type = 3; code = 4 } }
Write-Lf (Join-Path $Work 'ingress.json') (ConvertTo-Json $ingress -Depth 5)
OciOrThrow network security-list update --security-list-id $vcn.'default-security-list-id' --ingress-security-rules 'file:///work/ingress.json' --force | Out-Null

$subnet = (Oci network subnet list --compartment-id $Compartment --vcn-id $vcnId --display-name 'sat-subnet' --lifecycle-state AVAILABLE) | Select-Object -First 1
if (-not $subnet) {
    $subnet = OciOrThrow network subnet create --compartment-id $Compartment --vcn-id $vcnId --cidr-block '10.0.0.0/24' --display-name 'sat-subnet' --dns-label 'satsub' --wait-for-state AVAILABLE
    Write-Host "subnet created"
}
Write-Host "network ready (ports 22, 80, 443 open)"

# ---------------------------------------------------------------- SSH key
if (-not (Test-Path $KeyPath)) {
    Step "SSH key"
    & ssh-keygen -q -t ed25519 -f $KeyPath -N '""' -C 'sat-oracle'
    if ($LASTEXITCODE -ne 0) { throw "ssh-keygen failed" }
}
Copy-Item "$KeyPath.pub" (Join-Path $Work 'ssh.pub') -Force
Copy-Lf (Join-Path $PSScriptRoot 'oracle\cloud-init.yaml') (Join-Path $Work 'cloud-init.yaml')

# ---------------------------------------------------------------- Instance
Step "OCI: instance"
$instance = (Oci compute instance list --compartment-id $Compartment --display-name $Name) |
    Where-Object { $_.'lifecycle-state' -in 'RUNNING', 'STOPPED', 'STARTING', 'PROVISIONING', 'STOPPING' } | Select-Object -First 1
if ($instance) {
    Write-Host "instance exists: $($instance.id) [$($instance.shape), $($instance.'lifecycle-state')]"
    if ($instance.'lifecycle-state' -in 'STOPPED', 'STOPPING') {
        OciOrThrow compute instance action --action START --instance-id $instance.id --wait-for-state RUNNING | Out-Null
    }
} else {
    $attempts = @()
    if ($Shape -ne 'Micro') { $attempts += @{ shape = 'VM.Standard.A1.Flex'; ocpus = 4; mem = 24 }, @{ shape = 'VM.Standard.A1.Flex'; ocpus = 2; mem = 12 } }
    $images = @{}
    $deadline = (Get-Date).AddMinutes($CapacityRetryMinutes)
    $round = 0
    while (-not $instance) {
        $round++
        $list = @($attempts)
        $microNow = ($Shape -eq 'Micro') -or ($Shape -eq 'auto' -and (Get-Date) -gt $deadline)
        if ($microNow) { $list += @{ shape = 'VM.Standard.E2.1.Micro' } }
        if (-not $list) { throw "no shape to try" }
        foreach ($a in $list) {
            if (-not $images[$a.shape]) {
                $img = (Oci compute image list --compartment-id $Tenancy --operating-system 'Canonical Ubuntu' --operating-system-version '24.04' --shape $a.shape --sort-by TIMECREATED --sort-order DESC) |
                    Where-Object { $_.'display-name' -notmatch 'Minimal' } | Select-Object -First 1
                if (-not $img) { Write-Host "no Ubuntu 24.04 image for $($a.shape)" -ForegroundColor Yellow; continue }
                $images[$a.shape] = $img.id
            }
            foreach ($ad in @($ads)) {
                $cmd = @('compute', 'instance', 'launch', '--compartment-id', $Compartment, '--availability-domain', $ad.name, '--shape', $a.shape,
                    '--image-id', $images[$a.shape], '--subnet-id', $subnet.id, '--assign-public-ip', 'true', '--display-name', $Name,
                    '--ssh-authorized-keys-file', '/work/ssh.pub', '--user-data-file', '/work/cloud-init.yaml', '--boot-volume-size-in-gbs', '50',
                    '--wait-for-state', 'RUNNING', '--max-wait-seconds', '900')
                if ($a.ocpus) {
                    Write-Lf (Join-Path $Work 'shape.json') (ConvertTo-Json @{ ocpus = $a.ocpus; memoryInGBs = $a.mem })
                    $cmd += '--shape-config', 'file:///work/shape.json'
                }
                $label = if ($a.ocpus) { "$($a.shape) $($a.ocpus) OCPU / $($a.mem) GB" } else { $a.shape }
                Write-Host "try #${round}: $label in $($ad.name) ... " -NoNewline
                $r = Oci @cmd
                if ($r -and $r.id) { $instance = $r; Write-Host "OK" -ForegroundColor Green; break }
                $msg = ($script:OciError -split "`n" | Where-Object { $_ -match 'message|Out of|capacity|Limit' } | Select-Object -First 1)
                if (-not $msg) { $msg = ($script:OciError -split "`n" | Select-Object -Last 1) }
                Write-Host "no ($("$msg".Trim()))" -ForegroundColor Yellow
                if ($script:OciError -notmatch 'capacity|Capacity|LimitExceeded|TooManyRequests|InternalError|429|500') { throw "instance launch failed:`n$($script:OciError)" }
            }
            if ($instance) { break }
        }
        if (-not $instance) {
            if ((Get-Date) -gt $deadline -and ($Shape -ne 'auto' -or $microNow)) { throw "Oracle had no free capacity within $CapacityRetryMinutes minutes; run again later (or -Shape Micro / -CapacityRetryMinutes 120)" }
            Write-Host "Oracle has no free capacity right now; retrying in 60 s (until $($deadline.ToString('HH:mm')), then E2.1.Micro)" -ForegroundColor Yellow
            Start-Sleep -Seconds 60
        }
    }
    Write-Host "instance created: $($instance.id) [$($instance.shape)]"
}
$instanceId = $instance.id
$instanceShape = $instance.shape

$ip = $null
for ($i = 0; $i -lt 30 -and -not $ip; $i++) {
    $vnic = (Oci compute instance list-vnics --instance-id $instanceId) | Select-Object -First 1
    $ip = $vnic.'public-ip'
    if (-not $ip) { Start-Sleep -Seconds 5 }
}
if (-not $ip) { throw "the instance has no public IP" }
Write-Host "public ip: $ip"

# ---------------------------------------------------------------- Wait for SSH + cloud-init
Step "Waiting for SSH and the first-boot setup (Docker)"
$sshOpts = @('-i', $KeyPath, '-o', 'StrictHostKeyChecking=no', '-o', "UserKnownHostsFile=$KnownHosts", '-o', 'ConnectTimeout=10', '-o', 'ServerAliveInterval=30', '-o', 'LogLevel=ERROR')
$ready = $false
for ($i = 0; $i -lt 90; $i++) {
    & ssh @sshOpts "ubuntu@$ip" 'cloud-init status --wait >/dev/null 2>&1; docker compose version >/dev/null 2>&1 && id -nG | grep -q docker'
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Write-Host "." -NoNewline; Start-Sleep -Seconds 10
}
Write-Host ""
if (-not $ready) { throw "VM did not become reachable over SSH (ubuntu@$ip)" }

# ---------------------------------------------------------------- Settings (.env)
Step "Settings"
$state = @{}
if (Test-Path $StateFile) { Get-Content $StateFile | Where-Object { $_ -match '^\s*([A-Z_]+)\s*=\s*(.*)$' } | ForEach-Object { $state[$Matches[1]] = $Matches[2] } }
if (-not $state.POSTGRES_PASSWORD) { $state.POSTGRES_PASSWORD = Random-Secret 32 }
if (-not $state.CREATED) { $state.CREATED = (Get-Date).ToString('s') }
($state.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) | Set-Content $StateFile

if ($env:RENDER_API_KEY -and (-not $env:TELEGRAM_BOT_TOKEN -or -not $env:SAT_MIGRATE_FROM_URL -or -not $env:SAT_ADMIN_PHONE)) {
    try {
        $render = @{ Authorization = "Bearer $env:RENDER_API_KEY"; Accept = 'application/json' }
        $owner = (Invoke-RestMethod -Headers $render 'https://api.render.com/v1/owners?limit=20') | ForEach-Object { $_.owner } | Select-Object -First 1
        $svc = (Invoke-RestMethod -Headers $render "https://api.render.com/v1/services?ownerId=$($owner.id)&limit=50") | ForEach-Object { $_.service } | Where-Object { $_.name -eq $Name } | Select-Object -First 1
        if ($svc) {
            $ev = @{}; (Invoke-RestMethod -Headers $render "https://api.render.com/v1/services/$($svc.id)/env-vars?limit=50") | ForEach-Object { $ev[$_.envVar.key] = $_.envVar.value }
            if (-not $env:TELEGRAM_BOT_TOKEN -and $ev['Telegram__BotToken']) { $env:TELEGRAM_BOT_TOKEN = $ev['Telegram__BotToken']; Write-Host "telegram token: from Render" }
            if (-not $env:SAT_MIGRATE_FROM_URL -and $ev['DATABASE_URL']) { $env:SAT_MIGRATE_FROM_URL = $ev['DATABASE_URL']; Write-Host "old database: from Render (imported once, if the VM database is empty)" }
            if (-not $env:SAT_ADMIN_PHONE -and $ev['Seed__AdminPhone']) { $env:SAT_ADMIN_PHONE = $ev['Seed__AdminPhone'] }
        }
    } catch { Write-Host "Render lookup failed: $($_.Exception.Message)" -ForegroundColor Yellow }
}
if (-not $env:TELEGRAM_BOT_TOKEN) { Write-Host "TELEGRAM_BOT_TOKEN not set: Telegram codes/notifications will be off" -ForegroundColor Yellow }
$botUser = $env:TELEGRAM_BOT_USERNAME
if ($env:TELEGRAM_BOT_TOKEN -and -not $botUser) {
    try { $botUser = (Invoke-RestMethod "https://api.telegram.org/bot$($env:TELEGRAM_BOT_TOKEN)/getMe").result.username; Write-Host "telegram bot: @$botUser" } catch { Write-Host "telegram getMe failed (check the token)" -ForegroundColor Yellow }
}

$site = if ($Domain) { $Domain } else { "$($ip -replace '\.', '-').sslip.io" }
$envText = @"
SITE_ADDRESS=$site
POSTGRES_PASSWORD=$($state.POSTGRES_PASSWORD)
TELEGRAM_BOT_TOKEN=$($env:TELEGRAM_BOT_TOKEN)
TELEGRAM_BOT_USERNAME=$botUser
ADMIN_PHONE=$(if ($env:SAT_ADMIN_PHONE) { $env:SAT_ADMIN_PHONE } else { '+998900000000' })
ADMIN_PASSWORD=$($env:SAT_ADMIN_PASSWORD)
DEMO_PASSWORD=$($env:SAT_DEMO_PASSWORD)
SEED_DEMO=$(if ($env:SAT_SEED_DEMO) { $env:SAT_SEED_DEMO } else { 'false' })
CENTER_NAME=$(if ($env:SAT_CENTER_NAME) { $env:SAT_CENTER_NAME } else { 'SAT Center' })
MIGRATE_FROM_URL=$($env:SAT_MIGRATE_FROM_URL)
"@
Write-Lf (Join-Path $Work '.env') $envText
Copy-Lf (Join-Path $PSScriptRoot 'oracle\docker-compose.yml') (Join-Path $Work 'docker-compose.yml')
Copy-Lf (Join-Path $PSScriptRoot 'oracle\Caddyfile') (Join-Path $Work 'Caddyfile')
Copy-Lf (Join-Path $PSScriptRoot 'oracle\remote-setup.sh') (Join-Path $Work 'remote-setup.sh')
Write-Host "site: https://$site"

# ---------------------------------------------------------------- Source / image
Step "Packaging the source"
$tgz = Join-Path $Work 'src.tgz'
if (Test-Path $tgz) { Remove-Item $tgz }
& tar -czf $tgz -C $Root --exclude='bin' --exclude='obj' --exclude='.run' --exclude='TestResults' --exclude='appsettings.Local.json' SatPlatform Dockerfile
if ($LASTEXITCODE -ne 0) { throw "tar failed" }
Write-Host ("source: {0:N1} MB" -f ((Get-Item $tgz).Length / 1MB))

$mode = 'build'
$files = @($tgz, (Join-Path $Work '.env'), (Join-Path $Work 'docker-compose.yml'), (Join-Path $Work 'Caddyfile'), (Join-Path $Work 'remote-setup.sh'))
if ($instanceShape -notmatch 'A1') {
    # x86 with 1 GB RAM cannot build the image; build here and ship it.
    Step "Docker: building the image locally (x86 VM)"
    & docker build -t satplatform:deploy $Root
    if ($LASTEXITCODE -ne 0) { throw "docker build failed" }
    $tar = Join-Path $Work 'image.tar'; $gz = Join-Path $Work 'image.tar.gz'
    & docker save -o $tar satplatform:deploy
    if ($LASTEXITCODE -ne 0) { throw "docker save failed" }
    $in = [IO.File]::OpenRead($tar); $out = [IO.File]::Create($gz); $z = New-Object IO.Compression.GZipStream($out, [IO.Compression.CompressionLevel]::Fastest)
    $in.CopyTo($z); $z.Dispose(); $out.Dispose(); $in.Dispose(); Remove-Item $tar
    Write-Host ("image: {0:N0} MB" -f ((Get-Item $gz).Length / 1MB))
    $files += $gz
    $mode = 'load'
}

# ---------------------------------------------------------------- Upload + run
Step "Uploading to ubuntu@$ip"
& scp @sshOpts -q @files "ubuntu@${ip}:/opt/sat/"
if ($LASTEXITCODE -ne 0) { throw "scp failed" }

Step "Remote setup ($mode)"
& ssh @sshOpts "ubuntu@$ip" "chmod 600 /opt/sat/.env && bash /opt/sat/remote-setup.sh $mode"
if ($LASTEXITCODE -ne 0) { throw "remote setup failed (ssh -i deploy\oracle_ssh_key ubuntu@$ip, then: cd /opt/sat && docker compose logs)" }

# ---------------------------------------------------------------- Health
$url = "https://$site"
Step "Waiting for $url/healthz (TLS certificate is issued on first start)"
$up = $false
for ($i = 0; $i -lt 36; $i++) {
    try { if ((Invoke-WebRequest "$url/healthz" -UseBasicParsing -TimeoutSec 20).StatusCode -eq 200) { $up = $true; break } } catch { Write-Host "." -NoNewline }
    Start-Sleep -Seconds 10
}
Write-Host ""
if ($up) { Write-Host "SERVER IS UP: $url" -ForegroundColor Green } else { Write-Host "Not answering yet over HTTPS; check: ssh -i deploy\oracle_ssh_key ubuntu@$ip 'cd /opt/sat && docker compose logs caddy app'" -ForegroundColor Yellow }

@("SERVER_URL=$url", "PUBLIC_IP=$ip", "INSTANCE_ID=$instanceId", "SHAPE=$instanceShape", "SSH=ssh -i deploy\oracle_ssh_key ubuntu@$ip") | Set-Content (Join-Path $PSScriptRoot 'last-oracle-url.txt')
