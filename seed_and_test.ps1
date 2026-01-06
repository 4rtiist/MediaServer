# Konfiguration
$BaseUrl = "http://localhost:8080/api"
$ErrorActionPreference = "Stop"

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token = $null,
        [hashtable]$Body = $null
    )
    $Headers = @{}
    if ($Token) { $Headers["Authorization"] = "Bearer $Token" }
    
    $JsonBody = if ($Body) { $Body | ConvertTo-Json -Depth 10 } else { $null }
    
    try {
        $Uri = "$BaseUrl$Path"
        Write-Host "[$Method] $Uri" -ForegroundColor Cyan
        $Response = Invoke-RestMethod -Uri $Uri -Method $Method -Headers $Headers -Body $JsonBody -ContentType "application/json"
        return $Response
    } catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        # Optional: Detaillierte Fehlermeldung ausgeben
        # Write-Host $_.Exception.Response.GetResponseStream() 
    }
}

Write-Host "--- 1. REGISTER & LOGIN ---" -ForegroundColor Yellow

# User A (Creator)
$null = Invoke-Api "POST" "/users/register" -Body @{ username="Alice_Creator"; email="alice@test.com"; password="password123" }
$LoginA = Invoke-Api "POST" "/users/login" -Body @{ username="Alice_Creator"; password="password123" }
$TokenA = $LoginA.token
Write-Host "Alice Logged In. Token: $TokenA" -ForegroundColor Green

# User B (SciFi Fan)
$null = Invoke-Api "POST" "/users/register" -Body @{ username="Bob_Fan"; email="bob@test.com"; password="password123" }
$LoginB = Invoke-Api "POST" "/users/login" -Body @{ username="Bob_Fan"; password="password123" }
$TokenB = $LoginB.token
Write-Host "Bob Logged In." -ForegroundColor Green

# User C (Critic)
$null = Invoke-Api "POST" "/users/register" -Body @{ username="Charlie_Critic"; email="charlie@test.com"; password="password123" }
$LoginC = Invoke-Api "POST" "/users/login" -Body @{ username="Charlie_Critic"; password="password123" }
$TokenC = $LoginC.token
Write-Host "Charlie Logged In." -ForegroundColor Green

Write-Host "`n--- 2. CONTENT CREATION (Alice) ---" -ForegroundColor Yellow

# Filme erstellen
Invoke-Api "POST" "/media" -Token $TokenA -Body @{ title="Inception"; description="Dream hacker"; type=0; genre="SciFi"; releaseYear=2010; ageRestriction=12 }
Invoke-Api "POST" "/media" -Token $TokenA -Body @{ title="The Matrix"; description="Red pill or blue pill"; type=0; genre="SciFi"; releaseYear=1999; ageRestriction=16 }
Invoke-Api "POST" "/media" -Token $TokenA -Body @{ title="Interstellar"; description="Space travel"; type=0; genre="SciFi"; releaseYear=2014; ageRestriction=12 }
Invoke-Api "POST" "/media" -Token $TokenA -Body @{ title="The Witcher 3"; description="Monster hunter"; type=2; genre="Fantasy"; releaseYear=2015; ageRestriction=18 }
Invoke-Api "POST" "/media" -Token $TokenA -Body @{ title="Barbie"; description="Plastic world"; type=0; genre="Comedy"; releaseYear=2023; ageRestriction=0 }

Write-Host "`n--- 3. RATINGS & MODERATION ---" -ForegroundColor Yellow

# Bob bewertet Inception (5 Sterne, Kein Kommentar -> Auto-Confirm)
Invoke-Api "POST" "/ratings" -Token $TokenB -Body @{ mediaId=1; score=5 }

# Bob bewertet Matrix (4 Sterne, MIT Kommentar -> Pending)
$RatingResponse = Invoke-Api "POST" "/ratings" -Token $TokenB -Body @{ mediaId=2; score=4; comment="Really cool movie!" }
$PendingRatingId = $RatingResponse.rating.id

Write-Host "Bob rated Matrix with comment. Rating ID: $PendingRatingId. Status: Pending." -ForegroundColor Gray

# Alice (Creator) prüft Moderations-Queue
$PendingList = Invoke-Api "GET" "/media/moderation?mediaId=2" -Token $TokenA
Write-Host "Pending Items for Matrix: $($PendingList.Count)" -ForegroundColor Gray

# Alice genehmigt den Kommentar
Invoke-Api "POST" "/ratings/approve" -Token $TokenA -Body @{ ratingId=$PendingRatingId }
Write-Host "Alice approved the comment." -ForegroundColor Green

Write-Host "`n--- 4. SOCIAL & PROFILE ---" -ForegroundColor Yellow

# Alice liked Bobs Rating
Invoke-Api "POST" "/ratings/like" -Token $TokenA -Body @{ ratingId=$PendingRatingId }

# Bob favorisiert Witcher 3
Invoke-Api "POST" "/media/favorite" -Token $TokenB -Body @{ mediaId=4 }

# Profil Check (Bob)
$Profile = Invoke-Api "GET" "/users/profile" -Token $TokenB
Write-Host "Bob's Profile Stats:"
Write-Host "Total Ratings: $($Profile.totalRatings)"
Write-Host "Avg Score Given: $($Profile.averageScoreGiven)"
Write-Host "Fav Genre: $($Profile.favoriteGenre)"

Write-Host "`n--- 5. RECOMMENDATIONS (Bob) ---" -ForegroundColor Yellow
# Bob mag SciFi (Inception=5, Matrix=4). Er kennt Interstellar noch nicht.
# Erwartung: Interstellar wird empfohlen.

$Recs = Invoke-Api "GET" "/media/recommendations" -Token $TokenB
Write-Host "Recommendations for Bob:"
foreach ($r in $Recs) {
    Write-Host "- $($r.title) ($($r.genre))" -ForegroundColor Magenta
}

Write-Host "`n--- FERTIG! DATENBANK IST GEFÜLLT ---" -ForegroundColor Green