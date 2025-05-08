Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=MatrixCode&duration=-1"

Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=sonic&duration=5&cleanbg=false"

Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=sonic&duration=2&cleanbg=false&animation=ScrollLeft&queue"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=sonic&duration=1&cleanbg=false&animation=ScrollLeft&queue"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=sonic&duration=.5&cleanbg=false&animation=ScrollLeft&queue"
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/display/picture?path=sonic2&duration=2&cleanbg=false&animation=ScrollLeft&queue"
Start-Sleep -Seconds 8
Invoke-WebRequest -URI "http://127.0.0.1:8080/v1/exit"