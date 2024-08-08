Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=MatrixCode&duration=30&animation=Fade"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/score?players=2&activeplayer=1&score=99345&cleanbg=false"
Start-Sleep -Seconds 8
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/score?players=2&activeplayer=2&score=12376&cleanbg=false"