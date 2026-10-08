# One-shot deployment of the SAT platform to free hosting:
#   GitHub (code + Docker image in ghcr.io)  ->  Neon (PostgreSQL)  ->  Render (web service, free plan)
#
# Required environment variables (never stored in the repo):
#   $env:NEON_API_KEY   - console.neon.tech -> Account settings -> API keys
#   $env:RENDER_API_KEY - dashboard.render.com -> Account settings -> API keys
# Optional:
#   $env:GITHUB_TOKEN   - github.com -> Settings -> Developer settings -> Personal access tokens (classic), scopes: repo, write:packages.
#                         Without it the GitHub repo push is skipped; the image is pushed with the existing `docker login ghcr.io`
#                         and Render reuses its existing GitHub registry credential.
#   $env:GITHUB_USER    - GitHub account (default: diyorbekdev02-dev) when GITHUB_TOKEN is not set
#   $env:TELEGRAM_BOT_TOKEN    - bot token from @BotFather (registration codes + notifications)
#   $env:TELEGRAM_BOT_USERNAME - bot username without @ (resolved automatically from the token when omitted)
#   $env:SAT_ADMIN_PHONE       - super admin phone (default +998900000000)
#   $env:SAT_ADMIN_PASSWORD    - super admin password; random when omitted (see Render logs)
#   $env:SAT_DEMO_PASSWORD     - password of the demo accounts
#
# Usage:  powershell -File deploy\deploy.ps1          (re-runnable; existing resources are reused)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Root = Split-Path -Parent $PSScriptRoot
$RepoName = 'sat-platform'
$ServiceName = 'sat-platform'
$ImageName = 'sat-platform'

foreach ($v in 'NEON_API_KEY', 'RENDER_API_KEY') {
    if (-not (Get-Item "env:$v" -ErrorAction SilentlyContinue).Value) { throw "Missing environment variable $v" }
}

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

# ---------------------------------------------------------------- GitHub: repo + push
$login = if ($env:GITHUB_USER) { $env:GITHUB_USER } else { 'diyorbekdev02-dev' }
if ($env:GITHUB_TOKEN) {
    Step "GitHub: repository"
    $gh = @{ Authorization = "Bearer $env:GITHUB_TOKEN"; Accept = 'application/vnd.github+json'; 'User-Agent' = 'sat-deploy' }
    $login = (Invoke-RestMethod -Headers $gh 'https://api.github.com/user').login
    Write-Host "account: $login"
    try {
        $repo = Invoke-RestMethod -Headers $gh "https://api.github.com/repos/$login/$RepoName"
        Write-Host "repo exists: $($repo.html_url)"
    } catch {
        $repo = Invoke-RestMethod -Headers $gh -Method Post 'https://api.github.com/user/repos' -ContentType 'application/json' `
            -Body (@{ name = $RepoName; private = $true; description = 'Digital SAT preparation platform (Blazor + PostgreSQL)' } | ConvertTo-Json)
        Write-Host "repo created: $($repo.html_url)"
    }
    Step "GitHub: push"
    Push-Location $Root
    try {
        if (-not (Test-Path (Join-Path $Root '.git'))) { git init -b main | Out-Null }
        if (-not (git remote | Select-String -Quiet '^origin$')) { git remote add origin "https://github.com/$login/$RepoName.git" }
        else { git remote set-url origin "https://github.com/$login/$RepoName.git" }
        $basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("x-access-token:$env:GITHUB_TOKEN"))
        git -c "http.extraheader=AUTHORIZATION: basic $basic" push -u origin main
        if ($LASTEXITCODE -ne 0) { throw "git push failed" }
    } finally { Pop-Location }
} else {
    Write-Host "GITHUB_TOKEN not set: skipping the GitHub repo push (account assumed: $login)" -ForegroundColor Yellow
}

# ---------------------------------------------------------------- Docker image -> ghcr.io
Step "Docker: build + push image"
$imagePath = "ghcr.io/$($login.ToLower())/$ImageName`:latest"
docker build -t $imagePath $Root
if ($LASTEXITCODE -ne 0) { throw "docker build failed" }
if ($env:GITHUB_TOKEN) {
    docker login ghcr.io -u $login --password $env:GITHUB_TOKEN
    if ($LASTEXITCODE -ne 0) { throw "docker login failed" }
}
docker push $imagePath
if ($LASTEXITCODE -ne 0) { throw "docker push failed (run: docker login ghcr.io)" }

# ---------------------------------------------------------------- Neon: PostgreSQL
Step "Neon: database"
$neon = @{ Authorization = "Bearer $env:NEON_API_KEY"; Accept = 'application/json' }
$orgId = (Invoke-RestMethod -Headers $neon 'https://console.neon.tech/api/v2/users/me/organizations').organizations[0].id
$projects = (Invoke-RestMethod -Headers $neon "https://console.neon.tech/api/v2/projects?org_id=$orgId").projects
$project = $projects | Where-Object { $_.name -eq $RepoName } | Select-Object -First 1
if ($project) {
    Write-Host "project exists: $($project.id)"
    $branch = (Invoke-RestMethod -Headers $neon "https://console.neon.tech/api/v2/projects/$($project.id)/branches").branches | Where-Object { $_.default } | Select-Object -First 1
    $dbs = (Invoke-RestMethod -Headers $neon "https://console.neon.tech/api/v2/projects/$($project.id)/branches/$($branch.id)/databases").databases
    $db = $dbs | Select-Object -First 1
    $uri = (Invoke-RestMethod -Headers $neon "https://console.neon.tech/api/v2/projects/$($project.id)/connection_uri?branch_id=$($branch.id)&database_name=$($db.name)&role_name=$($db.owner_name)").uri
} else {
    $created = Invoke-RestMethod -Headers $neon -Method Post 'https://console.neon.tech/api/v2/projects' -ContentType 'application/json' `
        -Body (@{ project = @{ name = $RepoName; org_id = $orgId; region_id = 'aws-eu-central-1'; pg_version = 16 } } | ConvertTo-Json -Depth 5)
    $uri = $created.connection_uris[0].connection_uri
    Write-Host "project created: $($created.project.id)"
}
if (-not $uri) { throw "could not obtain the Neon connection string" }
Write-Host "database host: $(([Uri]$uri).Host)"

# ---------------------------------------------------------------- Telegram bot username
$botUser = $env:TELEGRAM_BOT_USERNAME
if ($env:TELEGRAM_BOT_TOKEN -and -not $botUser) {
    try { $botUser = (Invoke-RestMethod "https://api.telegram.org/bot$($env:TELEGRAM_BOT_TOKEN)/getMe").result.username; Write-Host "telegram bot: @$botUser" } catch { Write-Host "telegram getMe failed (check the token)" -ForegroundColor Yellow }
}

# ---------------------------------------------------------------- Render: web service
Step "Render: service"
$render = @{ Authorization = "Bearer $env:RENDER_API_KEY"; Accept = 'application/json' }
$owner = (Invoke-RestMethod -Headers $render 'https://api.render.com/v1/owners?limit=20') | ForEach-Object { $_.owner } | Select-Object -First 1
Write-Host "owner: $($owner.name) ($($owner.id))"

$creds = (Invoke-RestMethod -Headers $render "https://api.render.com/v1/registrycredentials?ownerId=$($owner.id)&limit=50") | ForEach-Object { if ($_.registryCredential) { $_.registryCredential } else { $_ } }
$cred = $creds | Where-Object { $_.name -eq 'ghcr-sat' } | Select-Object -First 1
if ($env:GITHUB_TOKEN) {
    $credBody = @{ registry = 'GITHUB'; username = $login; authToken = $env:GITHUB_TOKEN; name = 'ghcr-sat'; ownerId = $owner.id } | ConvertTo-Json
    if ($cred) { $cred = Invoke-RestMethod -Headers $render -Method Patch "https://api.render.com/v1/registrycredentials/$($cred.id)" -ContentType 'application/json' -Body $credBody }
    else { $cred = Invoke-RestMethod -Headers $render -Method Post 'https://api.render.com/v1/registrycredentials' -ContentType 'application/json' -Body $credBody }
} elseif (-not $cred) {
    $cred = $creds | Where-Object { $_.registry -eq 'GITHUB' } | Select-Object -First 1
    if (-not $cred) { throw "Render has no GitHub registry credential; set GITHUB_TOKEN" }
    Write-Host "reusing registry credential: $($cred.name)"
}

$services = (Invoke-RestMethod -Headers $render "https://api.render.com/v1/services?ownerId=$($owner.id)&limit=50") | ForEach-Object { $_.service }
$service = $services | Where-Object { $_.name -eq $ServiceName } | Select-Object -First 1

$envVars = @(
    @{ key = 'DATABASE_URL'; value = $uri },
    @{ key = 'App__KeepAlive'; value = 'true' },
    @{ key = 'ASPNETCORE_ENVIRONMENT'; value = 'Production' },
    @{ key = 'Seed__AdminPhone'; value = $(if ($env:SAT_ADMIN_PHONE) { $env:SAT_ADMIN_PHONE } else { '+998900000000' }) }
)
if ($env:TELEGRAM_BOT_TOKEN) { $envVars += @{ key = 'Telegram__BotToken'; value = $env:TELEGRAM_BOT_TOKEN } }
if ($botUser) { $envVars += @{ key = 'Telegram__BotUsername'; value = $botUser } }
if ($env:SAT_ADMIN_PASSWORD) { $envVars += @{ key = 'Seed__AdminPassword'; value = $env:SAT_ADMIN_PASSWORD } }
if ($env:SAT_DEMO_PASSWORD) { $envVars += @{ key = 'Seed__DemoPassword'; value = $env:SAT_DEMO_PASSWORD } }

if (-not $service) {
    $body = @{
        type = 'web_service'
        name = $ServiceName
        ownerId = $owner.id
        image = @{ ownerId = $owner.id; imagePath = $imagePath; registryCredentialId = $cred.id }
        envVars = $envVars
        serviceDetails = @{
            runtime = 'image'
            plan = 'free'
            region = 'frankfurt'
            healthCheckPath = '/healthz'
            pullRequestPreviewsEnabled = 'no'
        }
    } | ConvertTo-Json -Depth 6
    $service = (Invoke-RestMethod -Headers $render -Method Post 'https://api.render.com/v1/services' -ContentType 'application/json' -Body $body).service
    Write-Host "service created: $($service.id)"
} else {
    Write-Host "service exists: $($service.id)"
}

$url = $service.serviceDetails.url
if (-not $url) { $url = (Invoke-RestMethod -Headers $render "https://api.render.com/v1/services/$($service.id)").serviceDetails.url }
Write-Host "public URL: $url"

$envVars += @{ key = 'App__PublicUrl'; value = $url }
Invoke-RestMethod -Headers $render -Method Put "https://api.render.com/v1/services/$($service.id)/env-vars" -ContentType 'application/json' -Body (ConvertTo-Json $envVars -Depth 3) | Out-Null
if ($service.imagePath -or $service.image) {
    Invoke-RestMethod -Headers $render -Method Patch "https://api.render.com/v1/services/$($service.id)" -ContentType 'application/json' `
        -Body (@{ image = @{ ownerId = $owner.id; imagePath = $imagePath; registryCredentialId = $cred.id } } | ConvertTo-Json -Depth 4) | Out-Null
}
Invoke-RestMethod -Headers $render -Method Post "https://api.render.com/v1/services/$($service.id)/deploys" -ContentType 'application/json' -Body '{"clearCache":"do_not_clear"}' | Out-Null

Step "Waiting for the server to answer at $url/healthz"
$up = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 10
    try {
        if ((Invoke-WebRequest "$url/healthz" -UseBasicParsing -TimeoutSec 20).StatusCode -eq 200) { $up = $true; break }
    } catch { Write-Host "." -NoNewline }
}
Write-Host ""
if ($up) { Write-Host "SERVER IS UP: $url" -ForegroundColor Green } else { Write-Host "Not answering yet; check the Render dashboard logs." -ForegroundColor Yellow }

"SERVER_URL=$url" | Set-Content (Join-Path $PSScriptRoot 'last-url.txt')
