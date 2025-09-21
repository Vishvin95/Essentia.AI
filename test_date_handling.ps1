# Comprehensive User Context API Date Handling Test
Write-Host "=== Testing Enhanced Date Handling in User Context API ===" -ForegroundColor Cyan
Write-Host "Current Date: September 21, 2025 (Saturday)" -ForegroundColor Gray
Write-Host ""

# Test cases covering various date scenarios
$testCases = @(
    @{
        Name = "Specific Day Name - Monday"
        Text = "I have a meeting on Monday"
        Expected = "Should resolve to next Monday (Sept 22, 2025)"
    },
    @{
        Name = "Specific Day Name - Thursday" 
        Text = "Dentist appointment on Thursday"
        Expected = "Should resolve to next Thursday (Sept 25, 2025)"
    },
    @{
        Name = "Next Week Reference"
        Text = "I'll be traveling next week"
        Expected = "Should resolve to date range Sept 29 - Oct 5, 2025"
    },
    @{
        Name = "This Week Reference"
        Text = "Working from home this week" 
        Expected = "Should resolve to date range Sept 22 - Sept 28, 2025"
    },
    @{
        Name = "Tomorrow Reference"
        Text = "Call client tomorrow"
        Expected = "Should resolve to Sept 22, 2025"
    },
    @{
        Name = "Fuzzy Weekend Reference"
        Text = "There is a party on weekend!"
        Expected = "Should be fuzzy type, no specific dates"
    },
    @{
        Name = "Specific Day with Next"
        Text = "Meeting next Tuesday"
        Expected = "Should resolve to Sept 24, 2025"
    },
    @{
        Name = "Complex Date Range"
        Text = "Out of town for a week starting next Monday"
        Expected = "Should resolve to range starting Sept 22, 2025"
    }
)

$successCount = 0
$totalTests = $testCases.Count

foreach ($test in $testCases) {
    Write-Host "Test: $($test.Name)" -ForegroundColor Yellow
    Write-Host "Input: '$($test.Text)'" -ForegroundColor White
    Write-Host "Expected: $($test.Expected)" -ForegroundColor Gray
    
    $body = @{
        UserId = 1
        ContextText = $test.Text
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/process-context" -Method POST -Body $body -ContentType "application/json"
        
        Write-Host "✓ SUCCESS: Context processed" -ForegroundColor Green
        Write-Host "  Context ID: $($response.ContextId)" -ForegroundColor Cyan
        Write-Host "  Type: $($response.SavedContext.StructuredContext.Type)" -ForegroundColor Cyan
        
        # Show date references
        if ($response.SavedContext.StructuredContext.DateReferences -and $response.SavedContext.StructuredContext.DateReferences.Count -gt 0) {
            Write-Host "  Date References:" -ForegroundColor Magenta
            foreach ($dateRef in $response.SavedContext.StructuredContext.DateReferences) {
                Write-Host "    - Original: '$($dateRef.OriginalText)'" -ForegroundColor White
                Write-Host "    - Type: $($dateRef.DateType)" -ForegroundColor White
                Write-Host "    - Description: $($dateRef.RelativeDescription)" -ForegroundColor White
                if ($dateRef.ParsedDate) {
                    Write-Host "    - Parsed Date: $($dateRef.ParsedDate)" -ForegroundColor Green
                }
                if ($dateRef.StartDate -and $dateRef.EndDate) {
                    Write-Host "    - Date Range: $($dateRef.StartDate) to $($dateRef.EndDate)" -ForegroundColor Green
                }
                Write-Host "    - Confidence: $($dateRef.Confidence)" -ForegroundColor White
                Write-Host "    - Requires Context: $($dateRef.RequiresContext)" -ForegroundColor White
            }
        } else {
            Write-Host "  No date references found" -ForegroundColor Yellow
        }
        
        $successCount++
        Write-Host ""
        
    } catch {
        Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $responseBody = $reader.ReadToEnd()
            Write-Host "  Response: $responseBody" -ForegroundColor Red
        }
        Write-Host ""
    }
    
    # Wait between tests to avoid overwhelming the API
    Start-Sleep -Seconds 1
}

Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "Total Tests: $totalTests" -ForegroundColor White
Write-Host "Successful: $successCount" -ForegroundColor Green
Write-Host "Failed: $($totalTests - $successCount)" -ForegroundColor Red

if ($successCount -eq $totalTests) {
    Write-Host "🎉 All tests passed! Date handling is working correctly." -ForegroundColor Green
} else {
    Write-Host "⚠️  Some tests failed. Check the results above." -ForegroundColor Yellow
}